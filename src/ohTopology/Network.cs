﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

using OpenHome.Os;
using OpenHome.Os.App;
using OpenHome.Net.ControlPoint;

namespace OpenHome.Av
{
    public class WatchableScheduler : IDisposable
    {
        private readonly IWatchableThread iWatchableThread;
        private readonly DisposeHandler iDisposeHandler;
        private readonly object iLock;
        private Task iTask;

        public WatchableScheduler(IWatchableThread aWatchableThread)
        {
            iWatchableThread = aWatchableThread;
            iDisposeHandler = new DisposeHandler();
            iLock = new object();
            iTask = Task.Factory.StartNew(() => { });
        }

        public void Schedule(Action aAction)
        {
            using (iDisposeHandler.Lock)
            {
                lock (iLock)
                {
                    iTask = iTask.ContinueWith((t) =>
                    {
                        using (var done = new ManualResetEvent(false))
                        {
                            iWatchableThread.Schedule(() =>
                            {
                                try
                                {
                                    aAction.Invoke();
                                }
                                catch
                                {
                                }

                                done.Set();
                            });

                            done.WaitOne();
                        }
                    });
                }
            }
        }

        public void Wait()
        {
            using (iDisposeHandler.Lock)
            {
                lock (iLock)
                {
                    iTask.Wait();
                }
            }
        }

        // IDisposable

        public void Dispose()
        {
            iDisposeHandler.Dispose();
            iTask.Wait();
        }
    }

    public abstract class DeviceInjector : IDisposable
    {
        private readonly Network iNetwork;
        private readonly ILog iLog;
        private readonly Dictionary<string, IInjectorDevice> iDeviceLookup;
        protected readonly DisposeHandler iDisposeHandler;
        protected readonly CpDeviceListUpnpServiceType iDeviceList;

        protected DeviceInjector(Network aNetwork, string aDomain, string aType, uint aVersion, ILog aLog)
        {
            iDisposeHandler = new DisposeHandler();
            iNetwork = aNetwork;
            iLog = aLog;
            iDeviceList = new CpDeviceListUpnpServiceType(aDomain, aType, aVersion, Added, Removed);
            iDeviceLookup = new Dictionary<string,IInjectorDevice>();
        }

        protected void Added(CpDeviceList aList, CpDevice aDevice)
        {
            if (!FilterOut(aDevice))
            {
                IInjectorDevice device = Create(iNetwork, aDevice);
                iDeviceLookup.Add(aDevice.Udn(), device);
                iNetwork.Add(device);
            }
        }

        protected void Removed(CpDeviceList aList, CpDevice aDevice)
        {
            IInjectorDevice device;

            string udn = aDevice.Udn();

            if (iDeviceLookup.TryGetValue(udn, out device))
            {
                iNetwork.Remove(device);
                iDeviceLookup.Remove(udn);
            }
        }

        protected virtual IInjectorDevice Create(INetwork aNetwork, CpDevice aDevice)
        {
            using (iDisposeHandler.Lock)
            {
                return (DeviceFactory.Create(aNetwork, aDevice, iLog));
            }
        }

        protected virtual bool FilterOut(CpDevice aCpDevice)
        {
            return false;
        }

        public void Refresh()
        {
            using (iDisposeHandler.Lock)
            {
                iDeviceList.Refresh();
            }
        }

        // IDisposable

        public void Dispose()
        {
            iDeviceList.Dispose();
            iDisposeHandler.Dispose();
        }
    }

    public class DeviceInjectorProduct : DeviceInjector
    {
        public DeviceInjectorProduct(Network aNetwork, ILog aLog)
            : base(aNetwork, "av.openhome.org", "Product", 1, aLog)
        {
        }
    }

    public class DeviceInjectorSender : DeviceInjector
    {
        public DeviceInjectorSender(Network aNetwork, ILog aLog)
            : base(aNetwork, "av.openhome.org", "Sender", 1, aLog)
        {
        }

        protected override bool FilterOut(CpDevice aCpDevice)
        {
            string value;
            return aCpDevice.GetAttribute("Upnp.Service.av-openhome-org.Product", out value);
        }
    }

    internal class InjectorDeviceAdapter : IInjectorDevice
    {
        private readonly IInjectorDevice iDevice;

        public InjectorDeviceAdapter(IInjectorDevice aDevice)
        {
            iDevice = aDevice;
        }

        public void Join(Action aAction)
        {
            iDevice.Join(aAction);
        }

        public void Unjoin(Action aAction)
        {
            iDevice.Unjoin(aAction);
        }

        public string Udn
        {
            get
            {
                return iDevice.Udn;
            }
        }

        public void Create<T>(Action<T> aCallback, IDevice aDevice) where T : IProxy
        {
            iDevice.Create<T>(aCallback, aDevice);
        }

        public bool HasService(Type aServiceType)
        {
            return iDevice.HasService(aServiceType);
        }

        public bool Wait()
        {
            return iDevice.Wait();
        }

        public void Execute(IEnumerable<string> aValue)
        {
            iDevice.Execute(aValue);
        }

        public void Dispose()
        {
        }
    }

    internal class InjectorDeviceMock : IMockable, IDisposable
    {
        private readonly IInjectorDevice iDevice;
        private IInjectorDevice iOn;

        public InjectorDeviceMock(IInjectorDevice aDevice)
        {
            iDevice = aDevice;
        }

        public IInjectorDevice On()
        {
            Do.Assert(iOn == null);
            iOn = new InjectorDeviceAdapter(iDevice);
            return iOn;
        }

        public IInjectorDevice Off()
        {
            Do.Assert(iOn != null);
            
            var on = iOn;
            iOn = null;

            return on;
        }

        public void Dispose()
        {
            iDevice.Dispose();
        }

        public void Execute(IEnumerable<string> aValue)
        {
            iDevice.Execute(aValue);
        }
    }

    public class DeviceInjectorMock : IMockable, IDisposable
    {
        private readonly Network iNetwork;
        private readonly ILog iLog;
        private readonly string iResourceRoot;
        private readonly Dictionary<string, InjectorDeviceMock> iMockDevices;

        public DeviceInjectorMock(Network aNetwork, string aResourceRoot, ILog aLog)
        {
            iNetwork = aNetwork;
            iResourceRoot = aResourceRoot;
            iMockDevices = new Dictionary<string, InjectorDeviceMock>();
        }

        public void Dispose()
        {
            iNetwork.Execute(() =>
            {
                foreach (var d in iMockDevices.Values)
                {
                    d.Dispose();
                }
            });
        }

        public void Execute(IEnumerable<string> aValue)
        {
            iNetwork.Execute(() =>
            {
                string command = aValue.First().ToLowerInvariant();

                if (command == "small")
                {
                    CreateAndAdd(DeviceFactory.CreateDsm(iNetwork, "4c494e4e-0026-0f99-1112-ef000004013f", "Sitting Room", "Klimax DSM", "Info Time Volume Sender", iLog));
                    CreateAndAdd(DeviceFactory.CreateMediaServer(iNetwork, "4c494e4e-0026-0f99-0000-000000000000", iResourceRoot, iLog));
                    return;
                }
                else if (command == "medium")
                {
                    CreateAndAdd(DeviceFactory.CreateDs(iNetwork, "4c494e4e-0026-0f99-1111-ef000004013f", "Kitchen", "Sneaky Music DS", "Info Time Volume Sender", iLog));
                    CreateAndAdd(DeviceFactory.CreateDsm(iNetwork, "4c494e4e-0026-0f99-1112-ef000004013f", "Sitting Room", "Klimax DSM", "Info Time Volume Sender", iLog));
                    CreateAndAdd(DeviceFactory.CreateDsm(iNetwork, "4c494e4e-0026-0f99-1113-ef000004013f", "Bedroom", "Kiko DSM", "Info Time Volume Sender", iLog));
                    CreateAndAdd(DeviceFactory.CreateDs(iNetwork, "4c494e4e-0026-0f99-1114-ef000004013f", "Dining Room", "Majik DS", "Info Time Volume Sender", iLog));
                    CreateAndAdd(DeviceFactory.CreateMediaServer(iNetwork, "4c494e4e-0026-0f99-0000-000000000000", iResourceRoot, iLog));
                    return;
                }
                else if (command == "large")
                {
                    throw new NotImplementedException();
                }
                else if (command == "create")
                {
                    IEnumerable<string> value = aValue.Skip(1);

                    string type = value.First();

                    value = value.Skip(1);

                    string udn = value.First();

                    if (type == "ds")
                    {
                        Create(DeviceFactory.CreateDs(iNetwork, udn, iLog));
                        return;
                    }
                    else if (type == "dsm")
                    {
                        Create(DeviceFactory.CreateDsm(iNetwork, udn, iLog));
                        return;
                    }
                }
                else if (command == "add")
                {
                    IEnumerable<string> value = aValue.Skip(1);

                    string udn = value.First();

                    InjectorDeviceMock device;
                    if (iMockDevices.TryGetValue(udn, out device))
                    {
                        iNetwork.Add(device.On());
                        return;
                    }
                }
                else if (command == "remove")
                {
                    IEnumerable<string> value = aValue.Skip(1);

                    string udn = value.First();

                    InjectorDeviceMock device;
                    if (iMockDevices.TryGetValue(udn, out device))
                    {
                        iNetwork.Remove(device.Off());
                        return;
                    }
                }
                else if (command == "destroy")
                {
                    IEnumerable<string> value = aValue.Skip(1);

                    string udn = value.First();

                    InjectorDeviceMock device;
                    if (iMockDevices.TryGetValue(udn, out device))
                    {
                        iMockDevices.Remove(udn);
                        device.Dispose();
                        return;
                    }
                }
                else if (command == "update")
                {
                    IEnumerable<string> value = aValue.Skip(1);

                    string udn = value.First();

                    InjectorDeviceMock device;
                    if (iMockDevices.TryGetValue(udn, out device))
                    {
                        device.Execute(value.Skip(1));
                        return;
                    }
                }

                throw new NotSupportedException();
            });
        }

        private InjectorDeviceMock Create(IInjectorDevice aDevice)
        {
            InjectorDeviceMock device = new InjectorDeviceMock(aDevice);
            iMockDevices.Add(aDevice.Udn, device);
            return device;
        }

        private void CreateAndAdd(IInjectorDevice aDevice)
        {
            InjectorDeviceMock device = Create(aDevice);
            iNetwork.Add(device.On());
        }
    }

    public class Device : IDevice, IDisposable
    {
        private readonly DisposeHandler iDisposeHandler;
        private readonly IInjectorDevice iDevice;

        public Device(IInjectorDevice aDevice)
        {
            iDisposeHandler = new DisposeHandler();
            iDevice = aDevice;
        }

        public string Udn
        {
            get
            {
                using (iDisposeHandler.Lock)
                {
                    return iDevice.Udn;
                }
            }
        }

        public void Create<T>(Action<T> aCallback) where T : IProxy
        {
            using (iDisposeHandler.Lock)
            {
                iDevice.Create<T>(aCallback, this);
            }
        }

        public void Join(Action aAction)
        {
            using (iDisposeHandler.Lock)
            {
                iDevice.Join(aAction);
            }
        }

        public void Unjoin(Action aAction)
        {
            using (iDisposeHandler.Lock)
            {
                iDevice.Unjoin(aAction);
            }
        }

        public void Dispose()
        {
            iDisposeHandler.Dispose();

            iDevice.Dispose();
        }

        /*internal IInjectorDevice Device
        {
            get
            {
                using (iDisposeHandler.Lock)
                {
                    return iDevice;
                }
            }
        }*/

        internal bool HasService(Type aServiceType)
        {
            using (iDisposeHandler.Lock)
            {
                return iDevice.HasService(aServiceType);
            }
        }

        internal bool Wait()
        {
            using (iDisposeHandler.Lock)
            {
                return iDevice.Wait();
            }
        }
    }

    public interface INetwork : IWatchableThread, IDisposable
    {
        IIdCache IdCache { get; }
        ITagManager TagManager { get; }
        IWatchableUnordered<IDevice> Create<T>() where T : IProxy;
    }

    public class Network : INetwork
    {
        private readonly List<Exception> iExceptions;
        private readonly IWatchableThread iThread;
        private readonly WatchableScheduler iScheduler;
        private readonly Action iDispose;
        private readonly DisposeHandler iDisposeHandler;
        private readonly IdCache iCache;
        private readonly ITagManager iTagManager;
        private readonly Dictionary<string, Device> iDevices;
        private readonly Dictionary<Type, WatchableUnordered<IDevice>> iDeviceLists;

        public Network(uint aMaxCacheEntries, ILog aLog)
        {
            iExceptions = new List<Exception>();
            iThread = new MockThread(ReportException);
            iScheduler = new WatchableScheduler(iThread);
            iDispose = () => { (iThread as MockThread).Dispose(); };
            iDisposeHandler = new DisposeHandler();
            iCache = new IdCache(aMaxCacheEntries);
            iTagManager = new TagManager();
            iDevices = new Dictionary<string, Device>();
            iDeviceLists = new Dictionary<Type, WatchableUnordered<IDevice>>();
        }

        public Network(IWatchableThread aWatchableThread, uint aMaxCacheEntries, ILog aLog)
        {
            iExceptions = new List<Exception>();
            iThread = aWatchableThread;
            iScheduler = new WatchableScheduler(iThread);
            iDispose = () => { };
            iDisposeHandler = new DisposeHandler();
            iCache = new IdCache(aMaxCacheEntries);
            iTagManager = new TagManager();
            iDevices = new Dictionary<string, Device>();
            iDeviceLists = new Dictionary<Type, WatchableUnordered<IDevice>>();
        }

        private void ReportException(Exception aException)
        {
            lock (iExceptions)
            {
                iExceptions.Add(aException);
            }
        }

        private bool WaitDevices()
        {
            bool complete = true;

            iScheduler.Wait();

            iThread.Execute(() =>
            {
                foreach (var device in iDevices.Values)
                {
                    complete &= device.Wait();
                }
            });

            return (complete);
        }

        public void Wait()
        {
            while (true)
            {
                while (!WaitDevices()) ;

                iThread.Execute();

                if (WaitDevices())
                {
                    break;
                }
            }
        }

        internal void Add(IInjectorDevice aDevice)
        {
            using (iDisposeHandler.Lock)
            {
                iScheduler.Schedule(() =>
                {
                    Device handler = new Device(aDevice);
                    if (iDevices.ContainsKey(handler.Udn))
                    {
                        handler.Dispose();
                        return;
                    }
                    iDevices.Add(handler.Udn, handler);

                    foreach (KeyValuePair<Type, WatchableUnordered<IDevice>> kvp in iDeviceLists)
                    {
                        if (aDevice.HasService(kvp.Key))
                        {
                            kvp.Value.Add(handler);
                        }
                    }
                });
            }
        }

        internal void Remove(IInjectorDevice aDevice)
        {
            using (iDisposeHandler.Lock)
            {
                iScheduler.Schedule(() =>
                {
                    Device handler;

                    if (iDevices.TryGetValue(aDevice.Udn, out handler))
                    {
                        foreach (KeyValuePair<Type, WatchableUnordered<IDevice>> kvp in iDeviceLists)
                        {
                            if (aDevice.HasService(kvp.Key))
                            {
                                kvp.Value.Remove(handler);
                            }
                        }

                        iDevices.Remove(handler.Udn);
                        Schedule(() =>
                        {
                            handler.Dispose();
                        });
                    }
                });
            }
        }

        public IWatchableUnordered<IDevice> Create<T>() where T : IProxy
        {
            using (iDisposeHandler.Lock)
            {
                Assert();

                Type key = typeof(T);

                WatchableUnordered<IDevice> list;

                if (iDeviceLists.TryGetValue(key, out list))
                {
                    return list;
                }
                else
                {
                    list = new WatchableUnordered<IDevice>(iThread);
                    iDeviceLists.Add(key, list);
                    foreach (Device d in iDevices.Values)
                    {
                        if (d.HasService(key))
                        {
                            list.Add(d);
                        }
                    }
                    return list;
                }
            }
        }

        public IIdCache IdCache
        {
            get
            {
                using (iDisposeHandler.Lock)
                {
                    return iCache;
                }
            }
        }

        public ITagManager TagManager
        {
            get
            {
                using (iDisposeHandler.Lock)
                {
                    return (iTagManager);
                }
            }
        }

        // IWatchableThread

        public void Assert()
        {
            iThread.Assert();
        }

        public void Schedule(Action aAction)
        {
            iThread.Schedule(aAction);
        }

        public void Execute(Action aAction)
        {
            iThread.Execute(aAction);
        }

        // IDisposable

        public void Dispose()
        {
            Wait();

            iScheduler.Dispose();

            foreach (WatchableUnordered<IDevice> list in iDeviceLists.Values)
            {
                list.Dispose();
            }

            Execute(() =>
            {
                foreach (var device in iDevices.Values)
                {
                    device.Dispose();
                }
            });

            iDisposeHandler.Dispose();

            iDispose();

            if (iExceptions.Count > 0)
            {
                throw (new AggregateException(iExceptions.ToArray()));
            }
        }
    }
}

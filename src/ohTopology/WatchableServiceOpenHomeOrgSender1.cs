﻿using System;
using System.Linq;
using System.Collections.Generic;

using OpenHome.Os.App;
using OpenHome.Net.ControlPoint.Proxies;

namespace OpenHome.Av
{
    public interface IServiceOpenHomeOrgSender1
    {
        IWatchable<bool> Audio { get; }
        IWatchable<string> Metadata { get; }
        IWatchable<string> Status { get; }
    }

    public abstract class Sender : IWatchableService, IServiceOpenHomeOrgSender1, IDisposable
    {
        protected Sender(string aId, IServiceOpenHomeOrgSender1 aService)
        {
            iId = aId;
            iService = aService;
        }

        public abstract void Dispose();

        public string Id
        {
            get
            {
                return iId;
            }
        }

        public string Type
        {
            get
            {
                return "AvOpenHomeSender1";
            }
        }

        public IWatchable<bool> Audio
        {
            get
            {
                return iService.Audio;
            }
        }

        public IWatchable<string> Metadata
        {
            get
            {
                return iService.Metadata;
            }
        }

        public IWatchable<string> Status
        {
            get
            {
                return iService.Status;
            }
        }

        public string Attributes
        {
            get
            {
                return iAttributes;
            }
        }

        public string PresentationUrl
        {
            get
            {
                return iPresentationUrl;
            }
        }

        private string iId;

        protected IServiceOpenHomeOrgSender1 iService;
        protected string iAttributes;
        protected string iPresentationUrl;
    }

    public class ServiceOpenHomeOrgSender1 : IServiceOpenHomeOrgSender1, IDisposable
    {
        public ServiceOpenHomeOrgSender1(IWatchableThread aThread, string aId, CpProxyAvOpenhomeOrgSender1 aService)
        {
            iLock = new object();
            iDisposed = false;

            lock (iLock)
            {
                iService = aService;

                iService.SetPropertyAudioChanged(HandleAudioChanged);
                iService.SetPropertyMetadataChanged(HandleMetadataChanged);
                iService.SetPropertyStatusChanged(HandleStatusChanged);

                iAudio = new Watchable<bool>(aThread, string.Format("Audio({0})", aId), iService.PropertyAudio());
                iMetadata = new Watchable<string>(aThread, string.Format("Metadata({0})", aId), iService.PropertyMetadata());
                iStatus = new Watchable<string>(aThread, string.Format("Status({0})", aId), iService.PropertyStatus());
            }
        }

        public void Dispose()
        {
            lock (iLock)
            {
                if (iDisposed)
                {
                    throw new ObjectDisposedException("ServiceOpenHomeOrgSender1.Dispose");
                }

                iService = null;

                iDisposed = true;
            }
        }

        public IWatchable<bool> Audio
        {
            get
            {
                return iAudio;
            }
        }

        public IWatchable<string> Metadata
        {
            get
            {
                return iMetadata;
            }
        }

        public IWatchable<string> Status
        {
            get
            {
                return iStatus;
            }
        }

        private void HandleAudioChanged()
        {
            lock (iLock)
            {
                if (iDisposed)
                {
                    return;
                }

                iAudio.Update(iService.PropertyAudio());
            }
        }

        private void HandleMetadataChanged()
        {
            lock (iLock)
            {
                if (iDisposed)
                {
                    return;
                }

                iMetadata.Update(iService.PropertyMetadata());
            }
        }

        private void HandleStatusChanged()
        {
            lock (iLock)
            {
                if (iDisposed)
                {
                    return;
                }

                iStatus.Update(iService.PropertyStatus());
            }
        }

        private object iLock;
        private bool iDisposed;

        private CpProxyAvOpenhomeOrgSender1 iService;

        private Watchable<bool> iAudio;
        private Watchable<string> iMetadata;
        private Watchable<string> iStatus;
    }

    public class MockServiceOpenHomeOrgSender1 : IServiceOpenHomeOrgSender1, IMockable, IDisposable
    {
        public MockServiceOpenHomeOrgSender1(IWatchableThread aThread, string aId, bool aAudio, string aMetadata, string aStatus)
        {
            iAudio = new Watchable<bool>(aThread, string.Format("Audio({0})", aId), aAudio);
            iMetadata = new Watchable<string>(aThread, string.Format("Metadata({0})", aId), aMetadata);
            iStatus = new Watchable<string>(aThread, string.Format("Status({0})", aId), aStatus);
        }

        public void Dispose()
        {
        }

        public IWatchable<bool> Audio
        {
            get
            {
                return iAudio;
            }
        }

        public IWatchable<string> Metadata
        {
            get
            {
                return iMetadata;
            }
        }

        public IWatchable<string> Status
        {
            get
            {
                return iStatus;
            }
        }

        public void Execute(IEnumerable<string> aValue)
        {
            string command = aValue.First().ToLowerInvariant();
            if (command == "audio")
            {
                IEnumerable<string> value = aValue.Skip(1);
                iAudio.Update(bool.Parse(value.First()));
            }
            else if (command == "metadata")
            {
                IEnumerable<string> value = aValue.Skip(1);
                iMetadata.Update(value.First());
            }
            else if (command == "status")
            {
                IEnumerable<string> value = aValue.Skip(1);
                iStatus.Update(value.First());
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private Watchable<bool> iAudio;
        private Watchable<string> iMetadata;
        private Watchable<string> iStatus;
    }

    public class WatchableSenderFactory : IWatchableServiceFactory
    {
        public WatchableSenderFactory(IWatchableThread aThread)
        {
            iThread = aThread;

            iService = null;
        }

        public void Subscribe(WatchableDevice aDevice, Action<IWatchableService> aCallback)
        {
            if (iService == null && iPendingService == null)
            {
                iPendingService = new CpProxyAvOpenhomeOrgSender1(aDevice.Device);
                iPendingService.SetPropertyInitialEvent(delegate
                {
                    iThread.Schedule(() =>
                    {
                        iService = new WatchableSender(iThread, string.Format("Sender({0})", aDevice.Udn), iPendingService);
                        iPendingService = null;
                        aCallback(iService);
                    });
                });
                iPendingService.Subscribe();
            }
        }

        public void Unsubscribe()
        {
            if (iPendingService != null)
            {
                iPendingService.Dispose();
                iPendingService = null;
            }

            if (iService != null)
            {
                iService.Dispose();
                iService = null;
            }
        }

        private CpProxyAvOpenhomeOrgSender1 iPendingService;
        private WatchableSender iService;
        private IWatchableThread iThread;
    }

    public class WatchableSender : Sender
    {
        public WatchableSender(IWatchableThread aThread, string aId, CpProxyAvOpenhomeOrgSender1 aService)
            : base(aId, new ServiceOpenHomeOrgSender1(aThread, aId, aService))
        {
            iAttributes = aService.PropertyAttributes();
            iPresentationUrl = aService.PropertyPresentationUrl();

            iCpService = aService;
        }

        public override void Dispose()
        {
            if (iCpService != null)
            {
                iCpService.Dispose();
            }
        }

        private CpProxyAvOpenhomeOrgSender1 iCpService;
    }

    public class MockWatchableSender : Sender, IMockable
    {
        public MockWatchableSender(IWatchableThread aThread, string aId, string aAttributes, bool aAudio, string aMetadata, string aPresentationUrl, string aStatus)
            : base(aId, new MockServiceOpenHomeOrgSender1(aThread, aId, aAudio, aMetadata, aStatus))
        {
            iAttributes = aAttributes;
            iPresentationUrl = aPresentationUrl;
        }

        public override void Dispose()
        {
        }

        public void Execute(IEnumerable<string> aValue)
        {
            string command = aValue.First().ToLowerInvariant();
            if (command == "audio" || command == "metadata" || command == "status")
            {
                MockServiceOpenHomeOrgSender1 i = iService as MockServiceOpenHomeOrgSender1;
                i.Execute(aValue);
            }
            else if (command == "attributes")
            {
                IEnumerable<string> value = aValue.Skip(1);
                iAttributes = value.First();
            }
            else if (command == "presentationurl")
            {
                IEnumerable<string> value = aValue.Skip(1);
                iPresentationUrl = value.First();
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
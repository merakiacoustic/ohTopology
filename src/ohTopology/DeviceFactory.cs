﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using OpenHome.Net.ControlPoint;
using OpenHome.Os.App;
using OpenHome.Os;

namespace OpenHome.Av
{
    public static class DeviceFactory
    {
        public static Device Create(INetwork aNetwork, CpDevice aDevice)
        {
            Device device = new Device(aDevice.Udn());
            string value;
            if (aDevice.GetAttribute("Upnp.Service.av-openhome-org.Product", out value))
            {
                if (uint.Parse(value) == 1)
                {
                    device.Add<IProxyProduct>(new ServiceProductNetwork(aNetwork, aDevice));
                }
            }
            if (aDevice.GetAttribute("Upnp.Service.av-openhome-org.Info", out value))
            {
                if (uint.Parse(value) == 1)
                {
                    device.Add<IProxyInfo>(new ServiceInfoNetwork(aNetwork, aDevice));
                }
            }
            if (aDevice.GetAttribute("Upnp.Service.av-openhome-org.Time", out value))
            {
                if (uint.Parse(value) == 1)
                {
                    device.Add<IProxyTime>(new ServiceTimeNetwork(aNetwork, aDevice));
                }
            }
            if (aDevice.GetAttribute("Upnp.Service.av-openhome-org.Sender", out value))
            {
                if (uint.Parse(value) == 1)
                {
                    device.Add<IProxySender>(new ServiceSenderNetwork(aNetwork, aDevice));
                }
            }
            if (aDevice.GetAttribute("Upnp.Service.av-openhome-org.Volume", out value))
            {
                if (uint.Parse(value) == 1)
                {
                    device.Add<IProxyVolume>(new ServiceVolumeNetwork(aNetwork, aDevice));
                }
            }
            if (aDevice.GetAttribute("Upnp.Service.av-openhome-org.Playlist", out value))
            {
                if (uint.Parse(value) == 1)
                {
                    device.Add<IProxyPlaylist>(new ServicePlaylistNetwork(aNetwork, aDevice));
                }
            }
            if (aDevice.GetAttribute("Upnp.Service.av-openhome-org.Radio", out value))
            {
                if (uint.Parse(value) == 1)
                {
                    device.Add<IProxyRadio>(new ServiceRadioNetwork(aNetwork, aDevice));
                }
            }
            if (aDevice.GetAttribute("Upnp.Service.av-openhome-org.Receiver", out value))
            {
                if (uint.Parse(value) == 1)
                {
                    device.Add<IProxyReceiver>(new ServiceReceiverNetwork(aNetwork, aDevice));
                }
            }
            return device;
        }

        public static Device CreateDs(INetwork aNetwork, string aUdn)
        {
            return CreateDs(aNetwork, aUdn, "Main Room", "Mock DS", "Info Time Volume Sender");
        }

        public static Device CreateDs(INetwork aNetwork, string aUdn, string aRoom, string aName, string aAttributes)
        {
            Device device = new Device(aUdn);
            // add a factory for each type of watchable service

            // product service
            List<SourceXml.Source> sources = new List<SourceXml.Source>();
            sources.Add(new SourceXml.Source("Playlist", "Playlist", true));
            sources.Add(new SourceXml.Source("Radio", "Radio", true));
            sources.Add(new SourceXml.Source("UPnP AV", "UpnpAv", false));
            sources.Add(new SourceXml.Source("Songcast", "Receiver", true));
            sources.Add(new SourceXml.Source("Net Aux", "NetAux", false));
            SourceXml xml = new SourceXml(sources.ToArray());

            device.Add<IProxyProduct>(new ServiceProductMock(aNetwork, aRoom, aName, 0, xml, true, aAttributes,
                "", "Linn Products Ltd", "Linn", "http://www.linn.co.uk",
                "", "Linn High Fidelity System Component", "Mock DS", "",
                "", "Linn High Fidelity System Component", ""));

            // volume service
            device.Add<IProxyVolume>(new ServiceVolumeMock(aNetwork, aUdn, 0, 15, 0, 0, false, 50, 100, 100, 1024, 100, 80));

            // info service
            device.Add<IProxyInfo>(new ServiceInfoMock(aNetwork, new InfoDetails(0, 0, string.Empty, 0, false, 0), new InfoMetadata(string.Empty, string.Empty), new InfoMetatext(string.Empty)));

            // time service
            device.Add<IProxyTime>(new ServiceTimeMock(aNetwork, 0, 0));

            // sender service
            device.Add<IProxySender>(new ServiceSenderMock(aNetwork, aAttributes, string.Empty, false, new SenderMetadata("<DIDL-Lite xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\" xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\"><item id=\"\" parentID=\"\" restricted=\"True\"><dc:title>Main Room:Mock DS</dc:title><res protocolInfo=\"ohz:*:*:u\">ohz://239.255.255.250:51972/" + aUdn + "</res><upnp:albumArtURI>http://10.2.10.27/images/Icon.png</upnp:albumArtURI><upnp:class>object.item.audioItem</upnp:class></item></DIDL-Lite>"), "Enabled"));

            // receiver service
            device.Add<IProxyReceiver>(new ServiceReceiverMock(aNetwork, string.Empty, "ohz:*:*:*,ohm:*:*:*,ohu:*.*.*", "Stopped", string.Empty));

            // radio service
            device.Add<IProxyRadio>(new ServiceRadioMock(aNetwork, 0, new List<uint>(), new InfoMetadata(), string.Empty, "Stopped", 100));

            // playlist service
            device.Add<IProxyPlaylist>(new ServicePlaylistMock(aNetwork, 0, new List<uint>(), false, false, "Stopped", string.Empty, 1000));

            return device;
        }

        public static Device CreateDsm(INetwork aNetwork, string aUdn)
        {
            return CreateDsm(aNetwork, aUdn, "Main Room", "Mock Dsm", "Info Time Volume Sender");
        }

        public static Device CreateDsm(INetwork aNetwork, string aUdn, string aRoom, string aName, string aAttributes)
        {
            Device device = new Device(aUdn);
            // add a factory for each type of watchable service

            // product service
            List<SourceXml.Source> sources = new List<SourceXml.Source>();
            sources.Add(new SourceXml.Source("Playlist", "Playlist", true));
            sources.Add(new SourceXml.Source("Radio", "Radio", true));
            sources.Add(new SourceXml.Source("UPnP AV", "UpnpAv", false));
            sources.Add(new SourceXml.Source("Songcast", "Receiver", true));
            sources.Add(new SourceXml.Source("Net Aux", "NetAux", false));
            sources.Add(new SourceXml.Source("Analog1", "Analog", true));
            sources.Add(new SourceXml.Source("Analog2", "Analog", true));
            sources.Add(new SourceXml.Source("Phono", "Analog", true));
            sources.Add(new SourceXml.Source("SPDIF1", "Digital", true));
            sources.Add(new SourceXml.Source("SPDIF2", "Digital", true));
            sources.Add(new SourceXml.Source("TOSLINK1", "Digital", true));
            sources.Add(new SourceXml.Source("TOSLINK2", "Digital", true));
            SourceXml xml = new SourceXml(sources.ToArray());

            device.Add<IProxyProduct>(new ServiceProductMock(aNetwork, aRoom, aName, 0, xml, true, aAttributes,
                "", "Linn Products Ltd", "Linn", "http://www.linn.co.uk",
                "", "Linn High Fidelity System Component", "Mock DSM", "",
                "", "Linn High Fidelity System Component", ""));

            // volume service
            device.Add<IProxyVolume>(new ServiceVolumeMock(aNetwork, aUdn, 0, 15, 0, 0, false, 50, 100, 100, 1024, 100, 80));

            // info service
            device.Add<IProxyInfo>(new ServiceInfoMock(aNetwork, new InfoDetails(0, 0, string.Empty, 0, false, 0), new InfoMetadata(string.Empty, string.Empty), new InfoMetatext(string.Empty)));

            // time service
            device.Add<IProxyTime>(new ServiceTimeMock(aNetwork, 0, 0));

            // sender service
            device.Add<IProxySender>(new ServiceSenderMock(aNetwork, aAttributes, string.Empty, false, new SenderMetadata("<DIDL-Lite xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\" xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\"><item id=\"\" parentID=\"\" restricted=\"True\"><dc:title>Main Room:Mock DSM</dc:title><res protocolInfo=\"ohz:*:*:u\">ohz://239.255.255.250:51972/" + aUdn + "</res><upnp:albumArtURI>http://10.2.10.27/images/Icon.png</upnp:albumArtURI><upnp:class>object.item.audioItem</upnp:class></item></DIDL-Lite>"), "Enabled"));

            // receiver service
            device.Add<IProxyReceiver>(new ServiceReceiverMock(aNetwork, string.Empty, "ohz:*:*:*,ohm:*:*:*,ohu:*.*.*", "Stopped", string.Empty));

            // radio service
            device.Add<IProxyRadio>(new ServiceRadioMock(aNetwork, 0, new List<uint>(), new InfoMetadata(), string.Empty, "Stopped", 100));

            // playlist service
            device.Add<IProxyPlaylist>(new ServicePlaylistMock(aNetwork, 0, new List<uint>(), false, false, "Stopped", string.Empty, 1000));
            
            return device;
        }
    }
}
﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Xml;

using OpenHome.Os.App;
using OpenHome.MediaServer;

namespace OpenHome.Av
{
    public interface IMediaValue
    {
        string Value { get; }
        IEnumerable<string> Values { get; }
    }

    public interface IMediaMetadata : IEnumerable<KeyValuePair<ITag, IMediaValue>>
    {
        IMediaValue this[ITag aTag] { get; }
    }

    public interface IMediaDatum : IMediaMetadata
    {
        IEnumerable<ITag> Type { get; }
    }

    public interface IMediaPlayable
    {
        void Play();
    }

    public interface IMediaPreset : IMediaPlayable
    {
        IMediaMetadata Metadata { get; }
    }

    public interface IVirtualFragment
    {
        uint Index { get; }
        uint Sequence { get; }
        IEnumerable<IMediaDatum> Data { get; }
    }

    public interface IVirtualSnapshot
    {
        uint Total { get; }
        uint Sequence { get; }
        IEnumerable<uint> AlphaMap { get; } // null if no alpha map
        Task<IVirtualFragment> Read(uint aIndex, uint aCount);
    }

    public interface IVirtualContainer
    {
        IWatchable<IVirtualSnapshot> Snapshot { get; }
    }

    public interface IWatchableFragment<T>
    {
        uint Index { get; }
        uint Sequence { get; }
        IEnumerable<T> Data { get; }
    }

    public interface IWatchableSnapshot<T>
    {
        uint Total { get; }
        uint Sequence { get; }
        IEnumerable<uint> AlphaMap { get; } // null if no alpha map
        Task<IWatchableFragment<T>> Read(uint aIndex, uint aCount);
    }

    public interface IWatchableContainer<T>
    {
        IWatchable<IWatchableSnapshot<T>> Snapshot { get; }
    }

    public class MediaServerValue : IMediaValue
    {
        private readonly string iValue;
        private readonly List<string> iValues;

        public MediaServerValue(string aValue)
        {
            iValue = aValue;
            iValues = new List<string>(new string[] { aValue });
        }

        public MediaServerValue(IEnumerable<string> aValues)
        {
            iValue = aValues.First();
            iValues = new List<string>(aValues);
        }

        // IMediaServerValue

        public string Value
        {
            get { return (iValue); }
        }

        public IEnumerable<string> Values
        {
            get { return (iValues); }
        }
    }

    public class MediaDictionary
    {
        protected Dictionary<ITag, IMediaValue> iMetadata;

        protected MediaDictionary()
        {
            iMetadata = new Dictionary<ITag, IMediaValue>();
        }

        protected MediaDictionary(IMediaMetadata aMetadata)
        {
            iMetadata = new Dictionary<ITag, IMediaValue>(aMetadata.ToDictionary(x => x.Key, x => x.Value));
        }

        public void Add(ITag aTag, string aValue)
        {
            IMediaValue value = null;

            iMetadata.TryGetValue(aTag, out value);

            if (value == null)
            {
                iMetadata[aTag] = new MediaServerValue(aValue);
            }
            else
            {
                iMetadata[aTag] = new MediaServerValue(value.Values.Concat(new string[] { aValue }));
            }
        }

        public void Add(ITag aTag, IMediaValue aValue)
        {
            IMediaValue value = null;

            iMetadata.TryGetValue(aTag, out value);

            if (value == null)
            {
                iMetadata[aTag] = aValue;
            }
            else
            {
                iMetadata[aTag] = new MediaServerValue(value.Values.Concat(aValue.Values));
            }
        }

        public void Add(ITag aTag, IMediaMetadata aMetadata)
        {
            var value = aMetadata[aTag];

            if (value != null)
            {
                Add(aTag, value);
            }
        }

        // IMediaServerMetadata

        public IMediaValue this[ITag aTag]
        {
            get
            {
                IMediaValue value = null;
                iMetadata.TryGetValue(aTag, out value);
                return (value);
            }
        }
    }

    public class MediaMetadata : MediaDictionary, IMediaMetadata
    {
        public MediaMetadata()
        {
        }

        // IEnumerable<KeyValuePair<ITag, IMediaServer>>

        public IEnumerator<KeyValuePair<ITag, IMediaValue>> GetEnumerator()
        {
            return (iMetadata.GetEnumerator());
        }

        // IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (iMetadata.GetEnumerator());
        }
    }

    public class MediaDatum : MediaDictionary, IMediaDatum
    {
        private readonly ITag[] iType;

        public MediaDatum(params ITag[] aType)
        {
            iType = aType;
        }

        public MediaDatum(IMediaMetadata aMetadata, params ITag[] aType)
            : base(aMetadata)
        {
            iType = aType;
        }

        // IMediaDatum Members

        public IEnumerable<ITag> Type
        {
            get { return (iType); }
        }

        // IEnumerable<KeyValuePair<ITag, IMediaServer>>

        public IEnumerator<KeyValuePair<ITag, IMediaValue>> GetEnumerator()
        {
            return (iMetadata.GetEnumerator());
        }

        // IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

    public class VirtualFragment : IVirtualFragment
    {
        private readonly uint iIndex;
        private readonly uint iSequence;
        private readonly IEnumerable<IMediaDatum> iData;

        public VirtualFragment(uint aIndex, uint aSequence, IEnumerable<IMediaDatum> aData)
        {
            iIndex = aIndex;
            iSequence = aSequence;
            iData = aData;
        }

        // IWatchableFragment<T>

        public uint Index
        {
            get { return (iIndex); }
        }

        public uint Sequence
        {
            get { return (iSequence); }
        }

        public IEnumerable<IMediaDatum> Data
        {
            get { return (iData); }
        }
    }

    public class WatchableFragment<T> : IWatchableFragment<T>
    {
        private readonly uint iIndex;
        private readonly uint iSequence;
        private readonly IEnumerable<T> iData;

        public WatchableFragment(uint aIndex, uint aSequence, IEnumerable<T> aData)
        {
            iIndex = aIndex;
            iSequence = aSequence;
            iData = aData;
        }

        // IWatchableFragment<T>

        public uint Index
        {
            get { return (iIndex); }
        }

        public uint Sequence
        {
            get { return (iSequence); }
        }

        public IEnumerable<T> Data
        {
            get { return (iData); }
        }
    }

    public static class MediaExtensions
    {
        public static string ToDidlLite(this ITagManager aTagManager, IMediaMetadata aMetadata)
        {
            if (aMetadata == null)
            {
                return string.Empty;
            }
            return aMetadata[aTagManager.System.Folder].Value;
        }

        public static IMediaMetadata FromDidlLite(this ITagManager aTagManager, string aMetadata)
        {
            MediaMetadata metadata = new MediaMetadata();

            if (!string.IsNullOrEmpty(aMetadata))
            {
                XmlDocument document = new XmlDocument();
                XmlNamespaceManager nsManager = new XmlNamespaceManager(document.NameTable);
                nsManager.AddNamespace("didl", "urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/");
                nsManager.AddNamespace("upnp", "urn:schemas-upnp-org:metadata-1-0/upnp/");
                nsManager.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");
                nsManager.AddNamespace("ldl", "urn:linn-co-uk/DIDL-Lite");

                try
                {
                    document.LoadXml(aMetadata);

                    string c = document.SelectSingleNode("/didl:DIDL-Lite/*/upnp:class", nsManager).FirstChild.Value;
                    if (c.Contains("audioItem"))
                    {
                        string uri = document.SelectSingleNode("/didl:DIDL-Lite/*/didl:res", nsManager).FirstChild.Value;
                        metadata.Add(aTagManager.Audio.Uri, uri);
                    }
                }
                catch (XmlException) { }
            }
            
            metadata.Add(aTagManager.System.Folder, aMetadata);
            
            return metadata;
        }
    }
}

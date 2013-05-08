﻿using System;

using OpenHome.Os.App;

namespace OpenHome.Av
{
    public class SourceControllerPlaylist : IWatcher<string>, ISourceController
    {
        public SourceControllerPlaylist(IWatchableThread aThread, ITopology4Source aSource, Watchable<bool> aHasSourceControl, Watchable<bool> aHasInfoNext, Watchable<IInfoMetadata> aInfoNext, Watchable<string> aTransportState, Watchable<bool> aCanPause, Watchable<bool> aCanSkip, Watchable<bool> aCanSeek)
        {
            iDisposed = false;

            iSource = aSource;

            iHasSourceControl = aHasSourceControl;
            iInfoNext = aInfoNext;
            iCanPause = aCanPause;
            iCanSeek = aCanSeek;
            iTransportState = aTransportState;

            aSource.Device.Create<ServicePlaylist>((IWatchableDevice device, ServicePlaylist playlist) =>
            {
                if (!iDisposed)
                {
                    iPlaylist = playlist;

                    aHasInfoNext.Update(true);
                    aCanSkip.Update(true);

                    iPlaylist.TransportState.AddWatcher(this);

                    iHasSourceControl.Update(true);
                }
                else
                {
                    playlist.Dispose();
                }
            });
        }

        public void Dispose()
        {
            if (iDisposed)
            {
                throw new ObjectDisposedException("SourceControllerPlaylist.Dispose");
            }

            if (iPlaylist != null)
            {
                iHasSourceControl.Update(false);

                iPlaylist.TransportState.RemoveWatcher(this);

                iPlaylist.Dispose();
                iPlaylist = null;
            }

            iHasSourceControl = null;
            iInfoNext = null;
            iCanPause = null;
            iCanSeek = null;
            iTransportState = null;

            iDisposed = true;
        }

        public void Play()
        {
            iPlaylist.Play(null);
        }

        public void Pause()
        {
            iPlaylist.Pause(null);
        }

        public void Stop()
        {
            iPlaylist.Stop(null);
        }

        public void Previous()
        {
            iPlaylist.Previous(null);
        }

        public void Next()
        {
            iPlaylist.Next(null);
        }

        public void Seek(uint aSeconds)
        {
            iPlaylist.SeekSecondsAbsolute(aSeconds, null);
        }

        public void SetRepeat(bool aValue)
        {
            iPlaylist.SetRepeat(aValue, null);
        }

        public void SetShuffle(bool aValue)
        {
            iPlaylist.SetShuffle(aValue, null);
        }

        public void ItemOpen(string aId, string aValue)
        {
            iTransportState.Update(aValue);
        }

        public void ItemUpdate(string aId, string aValue, string aPrevious)
        {
            iTransportState.Update(aValue);
        }

        public void ItemClose(string aId, string aValue)
        {
        }

        private bool iDisposed;

        private ITopology4Source iSource;
        private ServicePlaylist iPlaylist;

        private Watchable<bool> iHasSourceControl;
        private Watchable<IInfoMetadata> iInfoNext;
        private Watchable<bool> iCanPause;
        private Watchable<bool> iCanSeek;
        private Watchable<string> iTransportState;
    }
}

﻿using streamdeck_client_csharp.Events;
using System;

namespace streamdeck_client_csharp
{
    public class StreamDeckEventReceivedEventArgs<T> : EventArgs
    {
        public T Event { get; private set; }
        internal StreamDeckEventReceivedEventArgs(T evt)
        {
            this.Event = evt;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MagicOnion.Server.Hubs;
using Xpand.Extensions.Reactive.Transform;

namespace Xpand.XAF.Modules.Reactive.Logger.Hub{
    [UsedImplicitly]
    public class TraceEventHub : StreamingHubBase<ITraceEventHub, ITraceEventHubReceiver>,ITraceEventHub{
        static readonly ISubject<TraceEventMessage> TraceSubject=new Subject<TraceEventMessage>();

        public static IObservable<TraceEventMessage> Trace => TraceSubject;

        static readonly ISubject<Unit> ConnectingSubject=Subject.Synchronize(new Subject<Unit>());
        static readonly ISubject<Unit> DisconnectingSubject=Subject.Synchronize(new Subject<Unit>());
        private IGroup _group;
        private static IConnectableObservable<IList<ITraceEvent>> _listenerEvents;


        public static IObservable<Unit> Connecting => ConnectingSubject;

        public  Task ConnectAsync(){
            ConnectingSubject.OnNext(Unit.Default);
            
            return _listenerEvents.SelectMany(list => list)
                .Concat(Observable.Defer(() => ReactiveLoggerService.ListenerEvents))
                .TakeUntil(DisconnectingSubject)
                .Select(e => {
                    var message = (TraceEventMessage) e;
                    Broadcast(_group).OnTraceEvent(message);
                    TraceSubject.OnNext(message);
                    return Unit.Default;
                })
                .DoNotComplete()
                .ToTask();
        }
        
        protected override async ValueTask OnConnecting(){
            _group = await Group.AddAsync("global");
        }

        protected override async ValueTask OnDisconnected(){
            if (_group != null){
                DisconnectingSubject.OnNext(Unit.Default);
                await _group.RemoveAsync(Context);

            }
        }

        public static void Init(){
            _listenerEvents = ReactiveLoggerService.ListenerEvents.Buffer(ConnectingSubject).FirstAsync().Replay(1);
            _listenerEvents.Connect();
        }
    }
}
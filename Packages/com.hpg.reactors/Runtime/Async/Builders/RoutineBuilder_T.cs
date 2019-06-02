﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security;
using JetBrains.Annotations;
using Lib.DataFlow;
using Utility;
using Utility.Asserts;
using Debug = UnityEngine.Debug;

namespace Lib.Async
{
    public class RoutineBuilder<T>
    {
        Action _continuation;
        IBreakableAwaiter _innerAwaiter;

        [UsedImplicitly] public Routine<T> Task { get; private set; }

        RoutineBuilder()
        {
            Task = new Routine<T>(this);
        }

        internal void BreakCurrent()
        {
            var i = _innerAwaiter;
            _innerAwaiter = null;
            if (i == null) return;

            i.Unsub();
            i.BreakInner();
        }

        [UsedImplicitly]
        public static RoutineBuilder<T> Create() => new RoutineBuilder<T>();


        [UsedImplicitly]
        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            _continuation = stateMachine.MoveNext;
            _continuation.Invoke();
        }

        [UsedImplicitly]
        public void SetResult(T value)
        {
            Task.SetResult(value);
            if (Task._scope.Sub.Completed)
                return;

            Task.Complete.Pub.Next();
        }

        [UsedImplicitly]
        public void SetException(Exception e)
        {
            Debug.LogException(e);
            SchPub.PubError.Next(e);
            Task.Complete.Pub.Next();
        }

        [UsedImplicitly]
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            switch (awaiter)
            {
                case IBreakableAwaiter breakableAwaiter:
                    _innerAwaiter = breakableAwaiter;
                    awaiter.OnCompleted(_continuation);
                    break;
                case SelfScopeAwaiter selfScopeAwaiter:
                    selfScopeAwaiter.Value = Task._scope.Sub;
                    awaiter.OnCompleted(_continuation);
                    break;
                default:
                    Asr.Fail("passed unbreakable awaiter");
                    break;
            }
        }


        [SecuritySafeCritical, UsedImplicitly]
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine =>
            AwaitOnCompleted(ref awaiter, ref stateMachine);


        [UsedImplicitly]
        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            _continuation = stateMachine.MoveNext;
        }
    }
}
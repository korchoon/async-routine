using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Lib.DataFlow;
using Lib.Utility;

namespace Lib.Async
{
    [AsyncMethodBuilder(typeof(RoutineBuilder))]
    public sealed class Routine : IDisposable
    {
        internal IScope _scope;
        internal IDisposeWith<Exception> StopImmediately;
        Action _moveAllAwaiters;
        bool _isCompleted;
        internal IDisposable PubDispose;
        internal IScope<Exception> _onErr;

        public IScope Scope(IScope breakOn)
        {
            breakOn.OnDispose(() => StopImmediately.DisposeWith(RoutineStoppedException.Empty));
            breakOn.OnDispose(PubDispose.Dispose);
            return _scope;
        }

        internal Routine()
        {
            DR.Register(this);
            DR.Next(dr => dr.Ctor, StackTraceHolder.New(1), this);

            _moveAllAwaiters = Empty.Action();
            StopImmediately = new CatchQueue(out _onErr);
            PubDispose = React.Scope(out _scope);
            _scope.OnDispose(InnerDispose);
            _onErr.OnDispose(_ => PubDispose.Dispose());

//            DR.Next(dr => dr.SetScope, _scope, this);
        }


        void InnerDispose()
        {
            _isCompleted = true;
            PubDispose.Dispose();
            RoutineUtils.MoveNextAndClear(ref _moveAllAwaiters);
            DR.Next(dr => dr.Dispose, this);
        }

        void IDisposable.Dispose()
        {
            StopImmediately.DisposeWith(RoutineStoppedException.Empty);
            PubDispose.Dispose();
        }

        [UsedImplicitly]
        public Awaiter GetAwaiter()
        {
            var res = new Awaiter(this, _onErr, ref _moveAllAwaiters);
            DR.Next(d => d.GetAwaiter, res, this);
            return res;
        }

        public class Awaiter : ICriticalNotifyCompletion, IBreakableAwaiter
        {
            Routine _awaitableTask;
            Action _continuation;
            Option<Exception> _exception;

            public Awaiter(Routine par, IScope<Exception> onErr, ref Action onMoveNext)
            {
                DA.Register(this);
                _awaitableTask = par;
                _continuation = Empty.Action();
                onErr.OnDispose(_DisposeWith);
                onMoveNext += () => RoutineUtils.MoveNextAndClear(ref _continuation);
            }

            void _DisposeWith(Exception err)
            {
                _exception = err;
                RoutineUtils.MoveNextAndClear(ref _continuation);
            }

            [UsedImplicitly] public bool IsCompleted => _awaitableTask._isCompleted;

            [UsedImplicitly]
            public void GetResult()
            {
                if (_exception.TryGet(out var err)) throw err;
            }


            public void OnCompleted(Action continuation)
            {
                if (IsCompleted)
                {
                    DA.Next(da => da.OnCompleteImmediate, StackTraceHolder.New(1), this);
                    continuation.Invoke();
                    return;
                }

                DA.Next(da => da.OnCompleteLater, StackTraceHolder.New(1), this);
                _continuation += continuation;
            }

            public void UnsafeOnCompleted(Action continuation) => ((INotifyCompletion) this).OnCompleted(continuation);

            public void Break(Exception e)
            {
                if (_exception.HasValue) return;
                _exception = e;
                _awaitableTask.StopImmediately.DisposeWith(e);
                RoutineUtils.MoveNextAndClear(ref _continuation);
            }
        }

        public class DR : DebugTracer<DR, Routine>
        {
            public Action<StackTraceHolder> Ctor;
            public Action Dispose;
            public Action<IScope> SubscribeToScope;
            public Action<IScope> SetScope;
            public Action<Awaiter> GetAwaiter;
        }

        public class DA : DebugTracer<DA, Awaiter>
        {
            public Action AfterBreak;
            public Action GetResult;
            public Action<Exception> Thrown;
            public Action<StackTraceHolder> OnCompleteImmediate;
            public Action<StackTraceHolder> OnCompleteLater;
        }
    }
}
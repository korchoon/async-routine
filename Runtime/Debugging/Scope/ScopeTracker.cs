using System.Collections.Generic;
using Lib.DataFlow;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;

namespace Lib.Async
{
    public class ScopeTracker : OdinEditorWindow
    {
        [MenuItem("Tools/Scopes")]
        static void OpenWindow()
        {
            var window = GetWindow<ScopeTracker>();
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(700, 700);
        }

        [ShowInInspector] SortableEdList<Scope> _all;

        protected override void OnEnable()
        {
            _all = new SortableEdList<Scope>((scope, scope1) => -scope.List.Count + scope1.List.Count);
            _Scope.OnNew += OnNew;
        }

        void OnNew(_Scope t)
        {
            var sc = new Scope();
            _all.All.Add(sc);

            t.CtorStackTrace += msg => sc.Ctor = msg;
            t.AfterDispose += () =>
            {
                sc.Disposed = true;
                _all.All.Remove(sc);
            };
            t.OnDispose += i => sc.List.Add(i);
        }


        [InlineProperty, HideReferenceObjectPicker]
        public class Scope
        {
            public StackTraceHolder Ctor;
            public bool Disposed;

            public List<StackTraceHolder> List = new List<StackTraceHolder>();
        }
    }
}
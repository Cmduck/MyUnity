using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace Extensions
{
    public class CallbackCoroutine<T>
    {
        public Action<T> callback;

        private IEnumerator target;
        private static readonly GameObject go;
        private static readonly MonoBehaviour mono;

        static CallbackCoroutine()
        {
            go = new GameObject { isStatic = true, name = "[Unify]" };
            mono = go.AddComponent<MonoBehaviour>();
        }

        public CallbackCoroutine(IEnumerator target, Action<T> callback)
        {
            this.target = target;
            this.callback = callback;
        }

        public void Start()
        {
            mono.StartCoroutine(StartUnifyCoroutine());
        }

        private IEnumerator StartUnifyCoroutine()
        {
            while (target.MoveNext())
            {
                yield return target.Current;
            }

            callback.Invoke((T)target.Current);
        }
    }
}

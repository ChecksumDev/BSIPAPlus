using System;
using UnityEngine;

namespace IPA.Injector
{
    internal class Bootstrapper : MonoBehaviour
    {
        public void Awake()
        {
        }

        public void Start()
        {
            Destroy(gameObject);
        }

        public void OnDestroy()
        {
            Destroyed();
        }

        public event Action Destroyed = delegate { };
    }
}
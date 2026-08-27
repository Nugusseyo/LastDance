using System;
using UnityEngine;

namespace _Works.JYG._Scripts.UI
{
    public class BillboardUI : MonoBehaviour
    {
        private Canvas _canvas;
        private Camera _mainCam;

        private void Awake()
        {
            _mainCam = Camera.main;
            _canvas = GetComponent<Canvas>();
        }

        private void LateUpdate()
        {
            if (_mainCam != null && _canvas != null)
            {
                _canvas.transform.up = _mainCam.transform.up;
                _canvas.transform.forward = _mainCam.transform.forward;
            }
        }
    }
}

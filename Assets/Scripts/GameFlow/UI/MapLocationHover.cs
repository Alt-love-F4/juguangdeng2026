using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    public sealed class MapLocationHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform rect;
        private MapSceneController owner;
        public void Initialize(MapSceneController value) => owner = value;
        private Image image;
        private Vector3 baseScale;
        private bool hovered;

        private void Awake()
        {
            rect = transform as RectTransform;
            image = GetComponent<Image>();
            baseScale = rect.localScale;
        }

        private void EnsureReady()
        {
            if (rect == null) { rect = transform as RectTransform; image = GetComponent<Image>(); baseScale = rect.localScale; }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            EnsureReady();
            hovered = true;
            rect.localScale = baseScale * 1.08f;
            if (image != null) image.color = image.sprite != null ? Color.white : new Color(1, 1, 1, 0);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            rect.localScale = baseScale;
            if (image != null) image.color = image.sprite != null ? Color.white : new Color(1, 1, 1, 0);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureReady();
            rect.localScale = baseScale * .96f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EnsureReady();
            rect.localScale = hovered ? baseScale * 1.08f : baseScale;
        }

        private void OnDisable()
        {
            hovered = false;
            if (rect != null) rect.localScale = baseScale;
        }

    }

}

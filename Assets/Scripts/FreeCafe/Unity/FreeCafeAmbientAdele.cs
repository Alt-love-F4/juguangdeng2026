using System;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.FreeCafe
{
    /// <summary>A presentation-only companion: never creates a customer, visit, seat reservation or order.</summary>
    public sealed class FreeCafeAmbientAdele : MonoBehaviour, IPointerClickHandler
    {
        private FreeCafeRuntime app;
        private NarrativeConfiguration configuration;
        private SpriteRenderer visual;
        private BoxCollider2D hitbox;
        private int rotation;
        private bool hasChat;
        private bool showing;
        private const float FadeSeconds = .45f;
        // Back-left seat, away from the business sign and the foreground production entry.
        private const int SeatIndex = 1;
        public SpriteRenderer Visual => visual;
        public bool IsAvailable => showing;

        public void Initialize(FreeCafeRuntime owner, NarrativeConfiguration source)
        {
            app = owner;
            configuration = source;
            hasChat = NarrativeCafeAmbientContent.TryCreateAdeleChat(source, 0, out _);
            var seating = app.seating;
            if (seating == null || seating.tables == null || seating.tables.Length <= SeatIndex) return;
            CafeSeatedArtwork artwork = Array.Find(seating.characterArt, a => a != null && a.characterId == "adele" && a.sprite != null);
            if (artwork == null) return;
            visual = gameObject.AddComponent<SpriteRenderer>();
            visual.sprite = artwork.sprite;
            visual.color = new Color(1, 1, 1, 0);
            visual.sortingLayerID = seating.background.sortingLayerID;
            visual.sortingOrder = seating.tables[SeatIndex].sortingOrder + 1;
            var area = app.areaRoots[(int)AreaId.Cafe];
            transform.position = app.AreaToWorld(AreaId.Cafe, seating.SeatPosition(SeatIndex).x, seating.SeatPosition(SeatIndex).y);
            Vector3 scale = seating.ArtworkScale(seating.tables[SeatIndex].perspectiveScale);
            if (seating.seatChairs != null && seating.seatChairs.Length > SeatIndex && seating.seatChairs[SeatIndex] != null)
            {
                var chair = seating.seatChairs[SeatIndex];
                scale = new Vector3(area.InverseTransformVector(chair.transform.TransformVector(Vector3.right)).magnitude,
                    area.InverseTransformVector(chair.transform.TransformVector(Vector3.up)).magnitude, 1);
                visual.flipX = chair.flipX != (chair.transform.lossyScale.x < 0);
            }
            else visual.flipX = seating.tables[SeatIndex].firstSeatOnRight;
            transform.localScale = scale;
            transform.localPosition += new Vector3(artwork.artworkOffset.x * scale.x * (visual.flipX ? -1 : 1),
                artwork.artworkOffset.y * scale.y, 0);
            hitbox = gameObject.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = visual.sprite.bounds.size;
            var center = visual.sprite.bounds.center;
            hitbox.offset = new Vector2(visual.flipX ? -center.x : center.x, center.y);
            hitbox.enabled = false;
        }

        private bool ShouldShow()
        {
            if (app == null || !app.isActiveAndEnabled || app.World == null || app.World.State.open || app.CafeTutorialActive) return false;
            var runtime = NarrativeRuntime.Instance;
            var controller = runtime != null ? runtime.Controller : null;
            if (!NarrativeCafeAmbientContent.IsAdeleAvailable(controller)) return false;
            // A story-authored Adele owns her appearance, even when the shop is closed.
            foreach (var visit in app.World.State.cafeVisits)
                if (visit.characterId == "adele" && visit.status != CafeVisitStatus.Ended) return false;
            foreach (var entity in app.World.State.entities)
                if (entity.person != null && entity.person.characterId == "adele") return false;
            return true;
        }

        private void Update()
        {
            if (visual == null) return;
            showing = ShouldShow();
            var color = visual.color;
            color.a = Mathf.MoveTowards(color.a, showing ? 1 : 0, Time.deltaTime / FadeSeconds);
            visual.color = color;
            visual.enabled = color.a > 0;
            hitbox.enabled = showing && hasChat && color.a > .05f && app.World.State.currentArea == AreaId.Cafe && !app.InputBlocked;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            if (app == null || app.InputBlocked || app.IsDragging || !ShouldShow() || !hasChat
                || app.World.State.currentArea != AreaId.Cafe) return;
            var runtime = NarrativeRuntime.Instance;
            Func<string, string> resolver = runtime != null && runtime.IsReady ? runtime.ResolveText : null;
            if (!NarrativeCafeAmbientContent.TryCreateAdeleChat(configuration, rotation, out var script, resolver)) return;
            DialoguePlayback.Play(script);
            rotation = (rotation + 1) % 2;
        }
    }
}

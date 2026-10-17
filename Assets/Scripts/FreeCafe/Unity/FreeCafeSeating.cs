using System;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    [Serializable]
    public sealed class CafeTablePlacement
    {
        public string id;
        public Vector2 tabletopPixel;
        public float perspectiveScale = 1f;
        public float seatOffsetPixels = 308f;
        public float seatDropPixels = 154f;
        public bool firstSeatOnRight;
        public int sortingOrder = 100;
    }

    [Serializable]
    public sealed class CafeSeatedArtwork
    {
        public string characterId;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class CafeIdleChair
    {
        public int tableIndex;
        public SpriteRenderer renderer;
    }

    public sealed class FreeCafeSeating : MonoBehaviour
    {
        public const string AcceptancePersonId = "seated_art_preview";
        public FreeCafeRuntime runtime;
        public SpriteRenderer background;
        public Vector2 sourceSize = new Vector2(4096, 2304);
        public CafeTablePlacement[] tables;
        public CafeIdleChair[] idleChairs;
        public SpriteRenderer[] seatChairs;
        public SpriteRenderer[] tableOccluders;
        public Sprite suppliedCustomer;
        public string suppliedGuestName = "佩杖绅士";
        public bool useSuppliedGuestForFirstSeat = true;
        public CafeSeatedArtwork[] characterArt = Array.Empty<CafeSeatedArtwork>();

        public Vector3 PixelToArea(Vector2 pixel)
        {
            var bounds = background.sprite.bounds;
            var local = new Vector3(bounds.min.x + pixel.x / sourceSize.x * bounds.size.x,
                bounds.max.y - pixel.y / sourceSize.y * bounds.size.y, 0);
            return runtime.areaRoots[(int)AreaId.Cafe].InverseTransformPoint(background.transform.TransformPoint(local));
        }

        public bool TrySeat(string customerId, out int index, out CafeTablePlacement table, out bool mirrored)
        {
            index = -1;
            table = null;
            mirrored = false;
            if (tables == null || tables.Length == 0 || string.IsNullOrEmpty(customerId)
                || !customerId.StartsWith("customer_", StringComparison.Ordinal)
                || !int.TryParse(customerId.Substring(9), out index) || index < 0 || index >= tables.Length * 2)
                return false;
            table = tables[index % tables.Length];
            mirrored = table.firstSeatOnRight != (index >= tables.Length);
            return true;
        }

        public Vector3 SeatPosition(int index)
        {
            var table = tables[index % tables.Length];
            bool mirrored = table.firstSeatOnRight != (index >= tables.Length);
            return PixelToArea(table.tabletopPixel + new Vector2((mirrored ? 1 : -1) * table.seatOffsetPixels,
                table.seatDropPixels) * table.perspectiveScale);
        }

        public void Configure(CafeCatalog catalog)
        {
            if (tables == null || tables.Length == 0 || background == null || background.sprite == null) return;
            catalog.customerSeats = new CafeSeatPosition[tables.Length * 2];
            catalog.economy.initialSeats = tables.Length;
            var bounds = Array.Find(catalog.areas, area => area.id == AreaId.Cafe);
            for (int index = 0; index < catalog.customerSeats.Length; index++)
            {
                var point = SeatPosition(index);
                catalog.customerSeats[index] = new CafeSeatPosition { x = point.x, y = point.y };
                if (bounds != null)
                {
                    bounds.halfWidth = Mathf.Max(bounds.halfWidth, Mathf.Abs(point.x) + 4f);
                    bounds.halfHeight = Mathf.Max(bounds.halfHeight, Mathf.Abs(point.y) + 5f);
                }
            }
        }

        public Sprite Artwork(WorldEntity entity)
        {
            if (entity.person == null) return null;
            if (entity.person.personId == AcceptancePersonId || entity.person.visualId == "supplied.seated.guest") return suppliedCustomer;
            foreach (var entry in characterArt)
                if (entry.characterId == entity.person.characterId && entry.sprite != null) return entry.sprite;
            return null;
        }

        public Vector3 ArtworkScale(float perspectiveScale)
        {
            var area = runtime.areaRoots[(int)AreaId.Cafe];
            return new Vector3(area.InverseTransformVector(background.transform.TransformVector(Vector3.right)).magnitude * perspectiveScale,
                area.InverseTransformVector(background.transform.TransformVector(Vector3.up)).magnitude * perspectiveScale, 1);
        }

        public bool Present(FreeEntityView view, WorldEntity entity)
        {
            if (entity.area != AreaId.Cafe || entity.specId != "customer"
                || !TrySeat(entity.id, out int index, out var table, out bool mirrored)) return false;
            view.transform.localScale = Vector3.one;
            view.TeleportPresentation(runtime.AreaToWorld(AreaId.Cafe, SeatPosition(index).x, SeatPosition(index).y));
            var sprite = view.body.sprite;
            if (sprite == null) return false;
            var scale = ArtworkScale(table.perspectiveScale);
            float fit = sprite == suppliedCustomer || Array.Exists(characterArt, entry => entry.sprite == sprite)
                ? 1f : 5.68f / Mathf.Max(.01f, sprite.bounds.size.y);
            scale.x *= fit;
            scale.y *= fit;
            view.body.transform.localScale = scale;
            view.body.flipX = mirrored;
            view.body.transform.localPosition = Artwork(entity) != null || sprite == suppliedCustomer
                ? Vector3.zero : new Vector3(0, sprite.bounds.size.y * scale.y * .21f, 0);
            view.body.sortingOrder = table.sortingOrder + 1;
            view.label.GetComponent<MeshRenderer>().sortingOrder = table.sortingOrder + 4;
            var size = Vector3.Scale(sprite.bounds.size, scale);
            var center = Vector3.Scale(sprite.bounds.center, scale);
            center.x *= mirrored ? -1 : 1;
            center += view.body.transform.localPosition;
            view.hitbox.size = new Vector2(size.x, size.y);
            view.hitbox.offset = new Vector2(center.x, center.y);
            view.label.transform.localPosition = new Vector3(0, center.y + size.y * .5f + .3f, -.1f);
            view.label.fontSize = 2.4f;
            view.label.gameObject.SetActive(view.IsHovered || runtime.SelectedId == entity.id);
            var person = view.GetComponent<FreeCafePersonView>();
            if (person != null && person.orderCaption != null)
                person.orderCaption.GetComponent<MeshRenderer>().sortingOrder = table.sortingOrder + 5;
            return true;
        }

        public void RefreshChairs()
        {
            if (runtime == null || runtime.World == null || tables == null) return;
            foreach (var chair in idleChairs)
            {
                bool occupied = runtime.World.Find("customer_" + chair.tableIndex) != null
                    || runtime.World.Find("customer_" + (chair.tableIndex + tables.Length)) != null;
                if (chair.renderer != null) chair.renderer.enabled = !occupied;
            }
            for (int index = 0; index < seatChairs.Length; index++)
            {
                int tableIndex = index % tables.Length;
                bool occupied = runtime.World.Find("customer_" + tableIndex) != null
                    || runtime.World.Find("customer_" + (tableIndex + tables.Length)) != null;
                if (seatChairs[index] != null) seatChairs[index].enabled = occupied;
                if (tableOccluders != null && tableIndex < tableOccluders.Length && tableOccluders[tableIndex] != null)
                    tableOccluders[tableIndex].enabled = occupied;
            }
        }

        public void SpawnAcceptanceCustomer()
        {
            if (runtime == null || runtime.World == null) return;
            var result = runtime.World.StartTutorialCustomer();
            if (!result.success) { runtime.Report(result); return; }
            var customer = runtime.World.Find(CafeWorld.TutorialCustomerId);
            customer.person = new CafePersonIdentity
            {
                personId = AcceptancePersonId, displayName = "坐姿素材验收顾客",
                role = CafePersonRole.Customer, visualId = "customer"
            };
            runtime.DebugRefreshViews();
        }

        public ICafeCustomerIdentitySource WithSuppliedGuest(ICafeCustomerIdentitySource fallback)
        {
            return useSuppliedGuestForFirstSeat && suppliedCustomer != null ? new GuestIdentitySource(this, fallback) : fallback;
        }

        private sealed class GuestIdentitySource : ICafeAvailableCustomerIdentitySource
        {
            private readonly FreeCafeSeating owner;
            private readonly ICafeCustomerIdentitySource fallback;

            public GuestIdentitySource(FreeCafeSeating owner, ICafeCustomerIdentitySource fallback)
            {
                this.owner = owner;
                this.fallback = fallback;
            }

            public CafePersonIdentity CreateIdentity(string visitId, int seatIndex)
            {
                return CreateIdentity(visitId, seatIndex, new System.Collections.Generic.HashSet<string>());
            }

            public CafePersonIdentity CreateIdentity(string visitId, int seatIndex, System.Collections.Generic.ISet<string> occupiedPersonIds)
            {
                if (seatIndex == 0 && !occupiedPersonIds.Contains("supplied_seated_guest"))
                    return new CafePersonIdentity { personId = "supplied_seated_guest", displayName = owner.suppliedGuestName,
                        role = CafePersonRole.Customer, visualId = "supplied.seated.guest" };
                return fallback is ICafeAvailableCustomerIdentitySource available
                    ? available.CreateIdentity(visitId, seatIndex, occupiedPersonIds) : fallback?.CreateIdentity(visitId, seatIndex);
            }
        }
    }
}

using System;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    [Serializable]
    public sealed class CafeTablePlacement
    {
        public string id;
        [Tooltip("原图左上角为原点的桌面中心像素；X 向右，Y 向下。不会移动底图中的桌子。")]
        public Vector2 tabletopPixel;
        [Tooltip("该桌顾客和座椅的透视缩放，也影响座椅相对桌面的偏移。")]
        public float perspectiveScale = 1f;
        [Tooltip("左右座位距离桌面中心的水平像素偏移，越大越远离桌子。")]
        public float seatOffsetPixels = 308f;
        [Tooltip("座位相对桌面中心向下的像素偏移，越大越靠近画面下方。")]
        public float seatDropPixels = 154f;
        [Tooltip("左侧座位单独修正，原图像素；X 向右，Y 向下，受透视缩放影响。")]
        public Vector2 leftSeatAdjustmentPixels;
        [Tooltip("右侧座位单独修正，原图像素；X 向右，Y 向下，受透视缩放影响。")]
        public Vector2 rightSeatAdjustmentPixels;
        [Tooltip("首个座位是否在桌子右侧；另一侧座位与其相反。")]
        public bool firstSeatOnRight;
        [Tooltip("桌椅排序基准；工作台为 200，此值应小于 195。后排默认 100，前排默认 150。")]
        public int sortingOrder = 100;
    }

    [Serializable]
    public sealed class CafeSeatedArtwork
    {
        public string characterId;
        public Sprite sprite;
        public Vector2 artworkOffset;
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
        [Tooltip("没有专用坐姿的顾客统一使用此黑影；不影响来客身份、订单或剧情。")]
        public Sprite silhouetteCustomer;
        public string suppliedGuestName = "佩杖绅士";
        public bool useSuppliedGuestForFirstSeat = true;
        public CafeSeatedArtwork[] characterArt = Array.Empty<CafeSeatedArtwork>();

        public bool CanPresentCustomer(string characterId) => silhouetteCustomer != null || HasCharacterArt(characterId);

        public bool HasCharacterArt(string characterId)
        {
            foreach (var entry in characterArt)
                if (entry != null && entry.characterId == characterId && entry.sprite != null) return true;
            return false;
        }

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
            var chair = SeatChair(index);
            if (chair != null)
                return runtime.areaRoots[(int)AreaId.Cafe].InverseTransformPoint(chair.transform.position);
            return CalculatedSeatPosition(index);
        }

        private SpriteRenderer SeatChair(int index)
        {
            return seatChairs != null && index >= 0 && index < seatChairs.Length ? seatChairs[index] : null;
        }

        private Vector3 CalculatedSeatPosition(int index)
        {
            var table = tables[index % tables.Length];
            bool mirrored = table.firstSeatOnRight != (index >= tables.Length);
            return PixelToArea(table.tabletopPixel + new Vector2((mirrored ? 1 : -1) * table.seatOffsetPixels,
                table.seatDropPixels) * table.perspectiveScale
                + (mirrored ? table.rightSeatAdjustmentPixels : table.leftSeatAdjustmentPixels) * table.perspectiveScale);
        }

        public void Configure(CafeCatalog catalog)
        {
            if (tables == null || tables.Length == 0 || background == null || background.sprite == null) return;
            catalog.customerSeats = new CafeSeatPosition[tables.Length * 2];
            catalog.customerTableCount = tables.Length;
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

        public Sprite Artwork(WorldEntity entity, Sprite visitSprite = null)
        {
            if (entity.specId != "customer") return null;
            if (entity.person == null) return visitSprite != null ? visitSprite : silhouetteCustomer;
            if (entity.person.personId == AcceptancePersonId || entity.person.visualId == "supplied.seated.guest") return suppliedCustomer;
            if (IsOrdinaryRandomGuest(entity.person)) return CatalogSilhouette(entity.person);
            foreach (var entry in characterArt)
                if (entry != null && entry.characterId == entity.person.characterId && entry.sprite != null) return entry.sprite;
            return visitSprite != null ? visitSprite : CatalogSilhouette(entity.person);
        }

        private Sprite CatalogSilhouette(CafePersonIdentity person)
        {
            // The tutorial guest has no Object ID; her identity is explicitly female in the source.
            if (person != null && person.personId == "tutorial_customer")
                return Resources.Load<Sprite>("Characters/silhouettes/female/seated_placeholder") ?? silhouetteCustomer;
            var character = person != null && runtime != null ? runtime.Character(person.characterId) : null;
            // Object indoor art carries the chosen source appearance; do not infer sex from display names.
            return character != null && character.worldSprite != character.portrait && character.worldSprite != null
                ? character.worldSprite : silhouetteCustomer;
        }

        private static bool IsOrdinaryRandomGuest(CafePersonIdentity person)
        {
            if (person == null || !string.IsNullOrEmpty(person.narrativeCharacterId)) return false;
            return person.characterId == "guest_a" || person.characterId == "guest_b"
                || person.characterId == "guest_c" || person.characterId == "guest_d";
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
            if (entity.alternateSeatSide)
            {
                index = (index + tables.Length) % (tables.Length * 2);
                mirrored = !mirrored;
            }
            view.transform.localScale = Vector3.one;
            view.TeleportPresentation(runtime.AreaToWorld(AreaId.Cafe, SeatPosition(index).x, SeatPosition(index).y));
            var sprite = view.body.sprite;
            if (sprite == null) return false;
            var scale = ArtworkScale(table.perspectiveScale);
            var chair = SeatChair(index);
            if (chair != null)
            {
                var area = runtime.areaRoots[(int)AreaId.Cafe];
                scale = new Vector3(area.InverseTransformVector(chair.transform.TransformVector(Vector3.right)).magnitude,
                    area.InverseTransformVector(chair.transform.TransformVector(Vector3.up)).magnitude, 1);
                mirrored = chair.flipX != (chair.transform.lossyScale.x < 0);
            }
            bool seatedArtwork = sprite == suppliedCustomer || sprite == silhouetteCustomer
                || (entity.person != null && sprite == CatalogSilhouette(entity.person))
                || Array.Exists(characterArt, entry => entry != null && entry.sprite == sprite);
            float fit = seatedArtwork
                ? 1f : 5.68f / Mathf.Max(.01f, sprite.bounds.size.y);
            scale.x *= fit;
            scale.y *= fit;
            view.body.transform.localScale = scale;
            view.body.flipX = mirrored;
            view.body.transform.localPosition = seatedArtwork
                ? Vector3.zero : new Vector3(0, sprite.bounds.size.y * scale.y * .21f, 0);
            foreach (var entry in characterArt)
                if (entry != null && entry.sprite == sprite && entity.person != null && entry.characterId == entity.person.characterId)
                {
                    view.body.transform.localPosition = new Vector3(entry.artworkOffset.x * scale.x * (mirrored ? -1 : 1),
                        entry.artworkOffset.y * scale.y, 0);
                    break;
                }
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
                if (chair.renderer != null)
                {
                    chair.renderer.sortingOrder = tables[chair.tableIndex].sortingOrder;
                    chair.renderer.enabled = !occupied;
                }
            }
            for (int index = 0; index < seatChairs.Length; index++)
            {
                int tableIndex = index % tables.Length;
                bool occupied = runtime.World.Find("customer_" + tableIndex) != null
                    || runtime.World.Find("customer_" + (tableIndex + tables.Length)) != null;
                if (seatChairs[index] != null)
                {
                    seatChairs[index].sortingOrder = tables[tableIndex].sortingOrder;
                    seatChairs[index].enabled = occupied;
                }
                if (tableOccluders != null && tableIndex < tableOccluders.Length && tableOccluders[tableIndex] != null)
                {
                    tableOccluders[tableIndex].sortingOrder = tables[tableIndex].sortingOrder + 3;
                    // Tables remain in front of chairs even when no customer is seated.
                    tableOccluders[tableIndex].enabled = true;
                }
            }
        }

        [ContextMenu("Reset Seat Chairs From Table Parameters")]
        public void RefreshSeatChairLayout()
        {
            if (runtime == null || background == null || background.sprite == null
                || tables == null || tables.Length == 0 || seatChairs == null) return;
            for (int index = 0; index < seatChairs.Length && index < tables.Length * 2; index++)
            {
                var chair = seatChairs[index];
                if (chair == null) continue;
                var table = tables[index % tables.Length];
                var point = CalculatedSeatPosition(index);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.RecordObjects(new UnityEngine.Object[] { chair.transform, chair }, "Reset cafe seat chair");
#endif
                chair.transform.position = runtime.AreaToWorld(AreaId.Cafe, point.x, point.y);
                chair.transform.localScale = ArtworkScale(table.perspectiveScale);
                chair.flipX = table.firstSeatOnRight != (index >= tables.Length);
                chair.sortingOrder = table.sortingOrder;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(chair.transform);
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(chair);
                }
#endif
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (runtime == null || background == null || background.sprite == null
                || tables == null || tables.Length == 0) return;
            for (int index = 0; index < tables.Length * 2; index++)
            {
                var point = SeatPosition(index);
                Gizmos.color = index < tables.Length ? Color.cyan : Color.yellow;
                Gizmos.DrawWireSphere(runtime.AreaToWorld(AreaId.Cafe, point.x, point.y), .2f);
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

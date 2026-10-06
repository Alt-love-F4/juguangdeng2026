using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Editor
{
    public static class FreeCafeInputDiagnostics
    {
        [MenuItem("Tools/Free Cafe/Diagnostics/Dump Pointer Routing")]
        public static void DumpPointerRouting()
        {
            var hud = Object.FindFirstObjectByType<FreeCafeHud>();
            var es = EventSystem.current;
            var lines = new List<string> { $"playing={Application.isPlaying} screen={Screen.width}x{Screen.height} focused={Application.isFocused}" };
            lines.Add($"editorWindow={EditorWindow.focusedWindow?.GetType().FullName}");
            foreach (var device in InputSystem.devices)
                lines.Add($"device={device} enabled={device.enabled} native={device.native} position={(device is Mouse m ? m.position.ReadValue().ToString() : "n/a")}");
            if (!InputSystem.devices.Any(d => d is Mouse && d.native))
                lines.Add("WARNING: Native mouse is missing. CLI virtual mice are not OS mice. If Simulator touch works but Game mouse does not, restart Unity to rediscover native devices; do not run bridge InputSystemHandlerTests in this editor session.");
            var runtime=UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if(runtime!=null)
            {
                var worldCamera=runtime.worldCamera;
                lines.Add($"worldCamera={worldCamera?.name} active={worldCamera!=null && worldCamera.isActiveAndEnabled} rect={worldCamera?.rect} pixelRect={worldCamera?.pixelRect}");
                var worldRaycaster=worldCamera!=null ? worldCamera.GetComponent<Physics2DRaycaster>() : null;
                lines.Add($"worldRaycaster={worldRaycaster?.name} enabled={worldRaycaster!=null && worldRaycaster.isActiveAndEnabled}");
                if(runtime.World!=null && es!=null && worldCamera!=null)
                {
                    Physics2D.SyncTransforms();
                    foreach(var entity in runtime.World.State.entities)
                    {
                        if(entity.area!=runtime.World.State.currentArea || runtime.World.Locked(entity)) continue;
                        var view=runtime.View(entity.id);if(view==null) continue;
                        var screen=(Vector2)worldCamera.WorldToScreenPoint(view.transform.position);
                        var worldHits=new List<RaycastResult>();es.RaycastAll(new PointerEventData(es){position=screen},worldHits);
                        lines.Add($"entity={entity.id} collider={view.hitbox.enabled} screen={screen} hits={string.Join(",",worldHits.Select(h=>h.gameObject.name))}");
                    }
                }
            }
            var module = es != null ? es.GetComponent<InputSystemUIInputModule>() : null;
            if (module != null)
            {
                foreach (var reference in new[] { module.point, module.leftClick })
                {
                    var action = reference?.action;
                    lines.Add($"action={action} enabled={action?.enabled} controls={string.Join(",", action?.controls.Select(c => c.path) ?? Enumerable.Empty<string>())}");
                }
            }
            if (hud != null && es != null)
            {
                Canvas.ForceUpdateCanvases();
                var canvas = hud.GetComponent<Canvas>();
                var raycaster = hud.GetComponent<GraphicRaycaster>();
                var camera = raycaster.eventCamera;
                lines.Add($"canvas={canvas.renderMode} camera={camera?.name} rect={camera?.rect} pixelRect={camera?.pixelRect}");
                foreach (var button in new[] { hud.startButton }.Concat(hud.areaButtons))
                {
                    var position = RectTransformUtility.WorldToScreenPoint(camera, button.transform.position);
                    var hits = new List<RaycastResult>();
                    es.RaycastAll(new PointerEventData(es) { position = position }, hits);
                    lines.Add($"button={button.name} screen={position} viewport={camera?.ScreenToViewportPoint(position)} interactable={button.interactable} hits={string.Join(",", hits.Select(h => h.gameObject.name))}");
                }
                if (Mouse.current != null)
                {
                    var position = Mouse.current.position.ReadValue();
                    var hits = new List<RaycastResult>();
                    es.RaycastAll(new PointerEventData(es) { position = position }, hits);
                    lines.Add($"mouse={position} pressed={Mouse.current.leftButton.isPressed} hits={string.Join(",", hits.Select(h => h.gameObject.name))}");
                }
            }
            Directory.CreateDirectory(".unity");
            File.WriteAllLines(".unity/freecafe-pointer-routing.txt", lines);
            Debug.Log("[FreeCafe Input] " + string.Join("\n", lines));
        }
    }
}

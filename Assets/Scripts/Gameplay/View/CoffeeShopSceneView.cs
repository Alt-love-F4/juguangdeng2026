using UnityEngine;
namespace Spotlight.Gameplay
{
    /// <summary>Legacy art references retained for migration. Physical item entities now own cup and NPC presentation.</summary>
    public sealed class CoffeeShopSceneView : MonoBehaviour
    {
        public SpriteRenderer customerRenderer, cupEmptyRenderer, cupLatteRenderer, cupSmall, machineEspresso, machineGrinder, machineMilkPot, machineCocoaPot, machineMatchaGrinder, serveWindow;
        public SpriteRenderer ingCoffeeBeans, ingMilk, ingMatcha, ingChocolate, ingHoney, ingCream;
        public Sprite[] customerSprites;
        public float slideDuration = .6f, customerOffscreenX = 11f, machinePulseScale = 1.08f;
    }
}

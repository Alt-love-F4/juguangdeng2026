using System;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        public bool IsMilkPitcher(WorldEntity entity) => entity != null && entity.specId == "container.milk-pitcher";
        public bool IsLatteArtCup(WorldEntity entity) => IsContainer(entity) && entity.specId.StartsWith("cup.", StringComparison.Ordinal);
        public float MilkFoamVolume(WorldEntity entity)
        {
            float volume = 0;
            if (entity != null) foreach (var portion in entity.contents)
                if (portion.specId == "milkfoam") volume += portion.milliliters;
            return volume;
        }

        /// <summary>Completed latte-art component volume; the physical foam inventory remains milkfoam.</summary>
        public float LatteArtFoamVolume(WorldEntity cup) => cup != null && cup.latteArt ? MilkFoamVolume(cup) : 0;

        /// <summary>Discard only foam transferred in this active session. Nothing is returned to the pitcher.</summary>
        public bool ResetLatteArtFoam(string token)
        {
            if (!ProductionEnabled || string.IsNullOrEmpty(token) || token != State.production.miniGameToken
                || State.production.miniGameKind != "latte-art") return false;
            var session = State.production;
            var cup = Find(session.miniGameCupId);
            if (cup == null) return false;
            float discarded = 0;
            foreach (var portion in cup.contents)
                if (portion.specId == "milkfoam" && portion.latteArtSessionToken == token)
                    discarded += portion.milliliters;
            if (discarded <= 0) return true;
            cup.contents.RemoveAll(portion => portion.specId == "milkfoam" && portion.latteArtSessionToken == token);
            cup.production.spilledMl += discarded; // Explicit waste, not disappearance or a refund.
            session.miniGameFoamUsedMl = 0;
            cup.latteArt = false; cup.latteArtScore = 0; cup.latteArtPattern = null;
            RefreshContainerTemperature(cup);
            if (cup.contents.Count == 0) cup.production.phase = ProductionPhase.Empty;
            Notify();
            return true;
        }

        private bool MiniGameUses(WorldEntity entity) => ProductionEnabled && entity != null
            && !string.IsNullOrEmpty(State.production.miniGameToken)
            && (entity.id == State.production.miniGameCupId || entity.id == State.production.miniGamePitcherId);

        public ActionResult FrothMilk(string pitcherId)
        {
            var pitcher = Find(pitcherId);
            if (!ProductionEnabled || !IsMilkPitcher(pitcher) || Locked(pitcher) || MiniGameUses(pitcher)
                || pitcher.production.phase == ProductionPhase.Processing)
                return ActionResult.No("拉花杯当前不可打奶，请先停止加热或结束拉花");
            bool hasMilk = false;
            foreach (var portion in pitcher.contents)
            {
                hasMilk |= (portion.specId == "milk" || portion.specId == "hotmilk") && portion.milliliters > 0;
            }
            if (!hasMilk) return ActionResult.No(MilkFoamVolume(pitcher) > 0 ? "已有奶泡，拖到杯子上开始拉花" : "请先向拉花杯加入牛奶");
            foreach (var portion in pitcher.contents)
                if (portion.specId == "milk" || portion.specId == "hotmilk")
                {
                    if (portion.composition == null || portion.composition.Count == 0)
                        AddConstituent(portion, "milk", portion.grams, portion.milliliters);
                    portion.specId = "milkfoam";
                }
            RefreshContainerTemperature(pitcher);
            Notify();
            return ActionResult.Yes("牛奶已打成奶泡 · " + MilkFoamVolume(pitcher).ToString("0.0") + " ml；拖到杯子上开始拉花");
        }

        public ActionResult PreviewMilkPitcherLatteArt(WorldEntity pitcher, WorldEntity cup)
        {
            if (!ProductionEnabled || !IsMilkPitcher(pitcher) || !IsLatteArtCup(cup) || pitcher.area != cup.area
                || Locked(pitcher) || Locked(cup) || pitcher.production.phase == ProductionPhase.Processing
                || cup.production.phase == ProductionPhase.Processing)
                return ActionResult.No("请将可用的拉花杯拖到同区域的杯子上");
            if (!string.IsNullOrEmpty(State.production.miniGameToken)) return ActionResult.No("请先结束当前拉花");
            if (MilkFoamVolume(pitcher) <= 0) return ActionResult.No("请先向拉花杯加入牛奶并点击打奶");
            if (LiquidVolume(cup) <= .001f) return ActionResult.No("空杯不能使用奶泡，请先加入饮品液体");
            if (LiquidCapacity(cup) - LiquidVolume(cup) <= .001f) return ActionResult.No("杯子已满，无法加入奶泡");
            return ActionResult.Yes("开始拉花 · 使用拉花杯内的奶泡，不进行普通倾倒");
        }

        public ActionResult RequestMilkPitcherLatteArt(string pitcherId, string cupId)
        {
            var pitcher = Find(pitcherId); var cup = Find(cupId);
            var preview = PreviewMilkPitcherLatteArt(pitcher, cup);
            if (!preview.success) return preview;
            var session = State.production;
            session.miniGameToken = Allocate("minigame_"); session.miniGameKind = "latte-art";
            session.miniGameCupId = cup.id; session.miniGamePitcherId = pitcher.id; session.miniGameFoamUsedMl = 0;
            Notify(); return preview;
        }

        public float ConsumeLatteArtFoam(string token, float requestedMl)
        {
            if (!ProductionEnabled || string.IsNullOrEmpty(token) || token != State.production.miniGameToken
                || !Finite(requestedMl) || requestedMl <= 0) return 0;
            var session = State.production;
            if (string.IsNullOrEmpty(session.miniGamePitcherId)) return requestedMl;
            var pitcher = Find(session.miniGamePitcherId); var cup = Find(session.miniGameCupId);
            if (!IsMilkPitcher(pitcher) || !IsLatteArtCup(cup) || pitcher.area != cup.area
                || !string.IsNullOrEmpty(pitcher.ownerId) || !string.IsNullOrEmpty(cup.ownerId)
                || LiquidVolume(cup) <= .001f) return 0;
            float available = MilkFoamVolume(pitcher);
            float accepted = Math.Min(requestedMl, Math.Min(available, Math.Max(0, LiquidCapacity(cup) - LiquidVolume(cup))));
            if (accepted <= 0) return 0;
            float fraction = accepted / available;
            foreach (var portion in pitcher.contents)
                if (portion.specId == "milkfoam")
                {
                    ReceiveContainerPortion(cup, portion, fraction, token);
                    ScalePortionAmounts(portion, 1 - fraction);
                }
            pitcher.contents.RemoveAll(portion => portion.milliliters <= 0 && portion.grams <= 0);
            RefreshContainerTemperature(pitcher); MixContainer(cup); RefreshContainerTemperature(cup);
            cup.latteArt = false; cup.latteArtScore = 0; cup.latteArtPattern = null;
            session.miniGameFoamUsedMl += accepted;
            return accepted;
        }
    }
}

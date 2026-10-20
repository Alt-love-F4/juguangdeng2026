"""Read-only economy estimate; not a playtest. Run with python -B.
Uses the actual catalog, deterministic menu rotation, opening batch and unlocks.
Costs are replacement cost at recommended strength, averaged 50/50 cup sizes;
no free opening stock credit, batching loss, remakes, tips or story gifts included.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def simulate(quality=0, served_limit=5, starting_money=500, reserve=0):
    c = json.loads((ROOT / "Assets/Resources/FreeCafe/catalog.json").read_text(encoding="utf-8"))
    products = c["products"]
    economy = c["economy"]
    capacities = {e["id"]: e.get("capacityMl", 0) for e in c["entities"]}
    money, reputation, successes, sequence = starting_money, 0, 0, 0
    precision = latte = False
    history = []

    def cost(p):
        total = 0
        for n in p["evaluation"]["amounts"]:
            k, ml, grams = n["ingredientId"], n.get("idealMl", 0), n.get("idealGrams", 0)
            total += (ml * .06 / 12 * economy["beanUnitPrices"][quality] if k == "coffee"
                      else ml / 200 * 5 if k in ("milk", "hotmilk", "milkfoam")
                      else ml / 200 * (5 if k == "matcha.drink" else 10) if k in ("matcha.drink", "cocoa.drink")
                      else grams / 12 * (10 if k == "cocoa.powder" else 5))
        return total * (capacities["cup.small"] + capacities["cup.large"]) / (2 * capacities[p["cupSpecId"]])

    def offer(p):
        ingredients = p["ingredients"]
        needs_precision = any(n.get("coffeeKind") == "essence" for n in ingredients)
        needs_latte = p.get("requiresLatteArt", False) or any(n["specId"] in ("milkfoam", "matcha.drink", "cocoa.drink") for n in ingredients)
        return successes >= p.get("unlockSuccessfulOrders", 0) and (precision or not needs_precision) and (latte or not needs_latte)

    def order():
        nonlocal sequence
        eligible = [p for p in products if offer(p)]
        p = eligible[sequence % len(eligible)]
        sequence += 1
        return p

    for day in range(1, 57):
        equipment = 0
        if not precision and successes >= 5 and money >= 200 + reserve:
            precision = True; money -= 200; equipment += 200
        if not latte and precision and successes >= 6 and money >= 350 + reserve:
            latte = True; money -= 350; equipment += 350
        budget = min(economy["maxCustomers"], economy["baseCustomers"] + max(0, reputation) // economy["customersReputationStep"])
        # Three seats are filled before any delivery can unlock another recipe.
        queue = [order() for _ in range(min(economy["initialSeats"], budget))]
        arrivals, served, revenue, spending = len(queue), 0, 0, 0
        while queue:
            p = queue.pop(0)
            if served < served_limit:
                revenue += p["price"] + (quality * p["qualityPriceStep"] if any(n["specId"] == "coffee" for n in p["ingredients"]) else 0)
                spending += cost(p)
                successes += 1; served += 1; reputation += 2
            if arrivals < budget:
                queue.append(order()); arrivals += 1
        target = min(10, max(1, arrivals))
        reputation += served if served >= target else served - target
        money += revenue - spending
        history.append(dict(day=day, balance=round(money, 2), served=served, revenue=revenue,
                            replacementCost=round(spending, 2), equipment=equipment,
                            net=round(revenue-spending, 2)))
    return history


if __name__ == "__main__":
    for quality in range(3):
        for limit in [3, 4, 5]:
            h = simulate(quality, limit)
            reached = next((r["day"] for r in h if r["balance"] >= 25000), None)
            print(json.dumps(dict(beanQuality=quality, dailyServedLimit=limit,
                matureNet=round(sum(r["net"] for r in h[10:54]) / 44, 2),
                day54Balance=h[53]["balance"], first25000Day=reached,
                checkpoints={str(d): h[d-1]["balance"] for d in [18,21,27,37,41,48,49,50,54]}), ensure_ascii=False))

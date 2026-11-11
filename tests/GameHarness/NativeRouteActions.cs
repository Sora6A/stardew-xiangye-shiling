using System.Reflection;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Menus;
using XiangyeShiling.GameIntegration;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>
/// 由已核对专用单人档的夹具在游戏主线程调用，使用当前玩家、原生商店与配方数据。
/// </summary>
internal static class NativeRouteActions
{
    internal static void Buy(IModHelper helper, IMonitor monitor, string shopId, string qualifiedId,
        int quantity, bool recipe = false)
    {
        var data = helper.GameContent.Load<Dictionary<string, ShopData>>("Data/Shops")[shopId];
        var shop = new ShopMenu(shopId, data, data.Owners.First(), playOpenSound: false);
        var item = shop.forSale.Single(i => i.QualifiedItemId == qualifiedId && i.IsRecipe == recipe);
        int price = shop.itemPriceAndStock[item].Price;
        monitor.Log($"Purchase {qualifiedId}: {quantity} at {price}g.", LogLevel.Info);
        Farmer player = Game1.player;
        int money = player.Money;
        // 保留商店原生库存、扣款与学习逻辑，夹具直接执行菜单的购买动作。
        var purchase = typeof(ShopMenu).GetMethod("tryToPurchaseItem", BindingFlags.Instance | BindingFlags.NonPublic)!;
        purchase.Invoke(shop, new object?[] { item, null, quantity, 0, 0 });
        Require(player.Money == money - price * quantity, "Shop didn't debit expected amount");
        if (recipe)
        {
            Require(player.knowsRecipe(((Item)item).Name), "Purchased recipe wasn't learned");
            return;
        }

        if (shop.heldItem is not Item held || held.QualifiedItemId != qualifiedId || held.Stack != quantity)
            throw new InvalidOperationException("Wrong purchased stack");
        Add(held);
        shop.heldItem = null;
    }

    internal static SObject Craft(string key, bool cooking = false)
    {
        var recipe = new CraftingRecipe(MachineService.Prefix + "_Recipe_" + key, cooking);
        Require(Game1.player.knowsRecipe(recipe.name) && recipe.doesFarmerHaveIngredientsInInventory(),
            "Recipe unlearned or missing ingredients");
        recipe.consumeIngredients(null);
        var output = (SObject)recipe.createItem();
        Add(output);
        return output;
    }

    private static void Add(Item item) => Require(Game1.player.addItemToInventory(item) is null,
        "Fixture inventory unexpectedly full");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

namespace STS.Game.UI
{
    /// <summary>
    /// 點藥水後跳出的小選單:使用 / 丟棄。
    /// 藥水欄只有三格,滿了就領不到新的,所以「倒掉」必須是玩家做得到的動作;
    /// 但倒掉不可逆,不能和「使用」一樣點一下就發生——中間隔一層選單。
    /// 全螢幕背板接住點在選單外的點擊當作取消。
    /// </summary>
    public sealed class PotionMenuView : MonoBehaviour
    {
        public static PotionMenuView Open(RectTransform overlayLayer, Vector2 anchorScreen, string potionName,
            Action onUse, Action onDiscard)
        {
            var backdrop = UiKit.CreatePanel("藥水選單", overlayLayer, new Color(0f, 0f, 0f, 0.4f));
            UiKit.Stretch(backdrop.rectTransform);
            backdrop.transform.SetAsLastSibling();
            var view = backdrop.gameObject.AddComponent<PotionMenuView>();

            // 背板本身就是「取消」——點選單以外的任何地方都關掉
            var cancel = backdrop.gameObject.AddComponent<Button>();
            cancel.onClick.AddListener(() => Destroy(backdrop.gameObject));

            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayLayer, anchorScreen, null, out var local);
            var panel = UiKit.CreatePanel("選單", backdrop.transform, new Color(0.16f, 0.14f, 0.22f, 0.98f));
            // 掛在藥水格正下方;選單自己不接受「點外面取消」,所以要蓋掉背板的點擊
            UiKit.Place(panel.rectTransform, local + new Vector2(0f, -150f), new Vector2(300f, 230f));
            panel.gameObject.AddComponent<Button>();   // 吃掉點擊,免得點在選單上也關掉

            UiKit.Place(UiKit.CreateText("瓶名", panel.transform, potionName, 26f,
                new Color(0.85f, 0.75f, 1f)).rectTransform, new Vector2(0f, 78f), new Vector2(280f, 40f));

            UiKit.Place((RectTransform)UiKit.CreateButton("使用", panel.transform, "使用", 28f,
                new Color(0.3f, 0.5f, 0.35f), () =>
                {
                    Destroy(backdrop.gameObject);
                    onUse();
                }).transform, new Vector2(0f, 16f), new Vector2(250f, 62f));

            UiKit.Place((RectTransform)UiKit.CreateButton("丟棄", panel.transform, "丟棄", 28f,
                new Color(0.5f, 0.27f, 0.27f), () =>
                {
                    Destroy(backdrop.gameObject);
                    onDiscard();
                }).transform, new Vector2(0f, -62f), new Vector2(250f, 62f));

            return view;
        }
    }
}

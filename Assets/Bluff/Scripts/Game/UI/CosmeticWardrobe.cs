using System;
using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bluff.Game
{
    public sealed class CosmeticWardrobe : MonoBehaviour
    {
        RectTransform root, body;
        Action changed;
        int tab, category, item, effect;
        int[] draft;
        PowerType power = PowerType.MataElang;
        string notice = "", nameDraft;
        InputField nameInput;
        ScenePreview preview;
        float characterYaw=-18f, characterZoom=1.15f;

        public static void Build(RectTransform parent, bool shop, Action onChanged)
        {
            var wardrobe = parent.gameObject.AddComponent<CosmeticWardrobe>();
            wardrobe.root = parent; wardrobe.changed = onChanged;
            wardrobe.tab = shop ? 1 : 0; wardrobe.draft = Cosmetics.Outfit;
            wardrobe.item = wardrobe.draft[0]; wardrobe.nameDraft = GameSettings.PlayerName;
            wardrobe.effect = Cosmetics.Effect(wardrobe.power); wardrobe.Render();
        }

        static void At(RectTransform rect, float x, float y, float width, float height) =>
            rect.Place(UIKit.TopLeft,new Vector2(x+width*.5f,-y-height*.5f),new Vector2(width,height));

        Text Label(Transform parent, string name, string text, float x, float y, float width, float height, int size = 22, Color? color = null)
        {
            var label = UIKit.CreateText(parent,name,text,size,color ?? Theme.Text,TextAnchor.MiddleLeft);
            label.supportRichText = false; label.verticalOverflow = VerticalWrapMode.Truncate;
            At(label.rectTransform,x,y,width,height); return label;
        }

        Button Button(Transform parent, string name, string text, float x, float y, float width, Action action, bool primary = false, GameIcon? icon = null, float height = 58f)
        {
            var button = UIKit.CreateButton(parent,name,text,new Vector2(width,height),primary ? Theme.Gold : Theme.PanelLight,primary ? Theme.TextDark : Theme.Text,action,22,8f);
            At((RectTransform)button.transform,x,y,width,height);
            if (icon.HasValue) UIKit.AddButtonIcon(button,SpriteFactory.Emblem(icon.Value),Color.white,36f,10f);
            return button;
        }

        void Render()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            string focus = selected != null && selected.transform.IsChildOf(root) ? selected.name : null;
            if (nameInput != null) nameDraft = nameInput.text;
            if(preview!=null && preview.IsCharacter) {characterYaw=preview.ViewYaw;characterZoom=preview.ViewZoom;}
            nameInput = null;
            foreach (Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            Canvas.ForceUpdateCanvases();
            float width = root.rect.width;
            string[] names = { "Profile", "Wardrobe", "Effects" };
            var icons = new[] { GameIcon.Profile, GameIcon.Costume, GameIcon.Effects };
            for (int i = 0; i < 3; i++)
            {
                int value = i;
                Button(root,"Wardrobe Tab " + i,names[i],i*222f,0f,210f,() => {
                    tab=value; notice=""; draft=Cosmetics.Outfit; item=category<5?draft[category]:0; Render();
                },tab==i,icons[i]);
            }
            var coin = UIKit.CreateImage(root,"Coin",SpriteFactory.Emblem(GameIcon.Coin),Color.white);
            At(coin.rectTransform,width-230f,6f,44f,44f);
            Label(root,"Wallet",Cosmetics.Coins + " coins",width-174f,0f,174f,58f,26,Theme.Gold);
            body = UIKit.CreateRect("Wardrobe Content",root); body.Stretch(0f,0f,80f,72f);
            if (tab==0) Profile(width); else if (tab==1) Wardrobe(width); else PowersView(width);
            var status = Label(root,"Store Notice",notice,0f,0f,width,38f,21,Theme.Gold);
            status.rectTransform.Place(UIKit.Bottom,new Vector2(0f,42f),new Vector2(width,38f));
            var earn = Label(root,"Earning","Finish a match: +40 coins. Win: +40 more.",0f,0f,width,30f,18,Theme.Muted);
            earn.rectTransform.Place(UIKit.Bottom,new Vector2(0f,10f),new Vector2(width,30f));
            var controls = new List<Selectable>();
            foreach (var control in root.GetComponentsInChildren<Selectable>()) if (control.IsInteractable()) controls.Add(control);
            var close = root.parent.Find("Close")?.GetComponent<Selectable>(); if (close != null) controls.Add(close);
            for (int i=0;i<controls.Count;i++) controls[i].navigation = new Navigation { mode=Navigation.Mode.Explicit,
                selectOnUp=controls[(i+controls.Count-1)%controls.Count],selectOnLeft=controls[(i+controls.Count-1)%controls.Count],
                selectOnDown=controls[(i+1)%controls.Count],selectOnRight=controls[(i+1)%controls.Count] };
            (controls.Find(control=>control.name==focus) ?? controls[0]).Select();
        }

        void Ownership(Button button, bool owned, bool equipped, bool selected)
        {
            button.targetGraphic.color=equipped ? Theme.Hex("365B45") : owned ? Theme.PanelLight : Theme.Hex("303333");
            var border=UIKit.CreateOutline(button.transform,"Preview selection",selected?Theme.Gold:equipped?Theme.Green:Theme.Border,8f);
            border.rectTransform.Stretch(); border.raycastTarget=false;
            var badge=UIKit.CreateImage(button.transform,"Ownership badge",SpriteFactory.Emblem(equipped?GameIcon.Check:owned?GameIcon.Costume:GameIcon.Lock),owned?Color.white:Theme.Muted);
            badge.rectTransform.Place(UIKit.TopRight,new Vector2(-20f,-20f),new Vector2(28f,28f));
            badge.raycastTarget=false;
        }

        RectTransform Stage(float x,float y,float width,float height)
        {
            var panel=UIKit.CreatePanel(body,"3D Stage",Theme.Background,12f,true);
            At(panel.rectTransform,x,y,width,height); panel.gameObject.AddComponent<RectMask2D>();
            return panel.rectTransform;
        }

        void Character(float y,float height)
        {
            preview=ScenePreview.Character(Stage(0f,y,400f,height),Cosmetics.Pack(draft));
            preview.SetView(characterYaw,characterZoom);
            Button(body,"Rotate left","<",0f,y+height+10f,64f,()=>preview.Rotate(-30f),false,null,44f);
            Button(body,"Reset view","Reset view",76f,y+height+10f,248f,()=>preview.ResetView(),false,null,44f);
            Button(body,"Rotate right",">",336f,y+height+10f,64f,()=>preview.Rotate(30f),false,null,44f);
        }

        void Profile(float width)
        {
            draft=Cosmetics.Outfit; Character(0f,486f);
            const float x=432f;
            Label(body,"Heading","Your seat at the table",x,12f,width-x,54f,34);
            Label(body,"Name Label","Character name",x,92f,width-x,36f,22,Theme.Muted);
            nameInput=UIKit.CreateInput(body,nameDraft,"Character name",24,new Vector2(width-x,68f));
            nameInput.name="Character Name"; At((RectTransform)nameInput.transform,x,140f,width-x,68f);
            Button(body,"Save Name","Save name",x,232f,256f,()=>{
                if (string.IsNullOrWhiteSpace(nameInput.text)) { notice="Enter a character name."; Render(); return; }
                GameSettings.PlayerName=nameInput.text; nameInput.text=GameSettings.PlayerName;
                notice="Name saved."; changed?.Invoke(); Render();
            },true,GameIcon.Profile);
            Button(body,"Open wardrobe","Customize outfit",x,328f,320f,()=>{tab=1;Render();},false,GameIcon.Costume);
            Label(body,"Preview hint","Drag to rotate. Scroll to zoom.",0f,546f,400f,32f,18,Theme.Muted);
        }

        void Wardrobe(float width)
        {
            var categories=new[] { "Jackets","Trousers","Shoes","Headwear","Eyewear","Bundles" };
            for (int i=0;i<categories.Length;i++)
            {
                int value=i;
                Button(body,"Category " + i,categories[i],i*(width/6f),0f,width/6f-8f,()=>{
                    category=value; item=category<5 ? draft[category] : 0; notice=""; Render();
                },category==i,null,48f);
            }
            int[] shown=category==5 ? Cosmetics.BundleOutfit(item) : draft;
            var saved=draft; draft=shown; Character(66f,430f); draft=saved;
            string[] items=category==5 ? Cosmetics.CostumeNames : Cosmetics.ItemNames[category];
            float column=(width-456f)/3f;
            for (int i=0;i<items.Length;i++)
            {
                int value=i;
                float x=432f+(i%3)*(column+12f), y=66f+(i/3)*150f;
                var pick=Button(body,"Item " + i,"",x,y,column,()=>{
                    item=value; if(category<5) draft[category]=item; notice=""; Render();
                },false,null,140f);
                var outfit=category==5 ? Cosmetics.BundleOutfit(i) : new[] {0,0,0,4,6};
                if(category<5) outfit[category]=i;
                var thumbnail=AvatarPortrait.Item(pick.transform,Cosmetics.Pack(outfit),category);
                At(thumbnail.rectTransform,(column-88f)*.5f,6f,88f,88f);
                Label(pick.transform,"Item name",items[i],9f,96f,column-18f,23f,17,Theme.Text);
                bool owned=category==5 ? Cosmetics.OwnsCostume(i) : Cosmetics.OwnsItem((WardrobeSlot)category,i);
                bool equipped=category==5 ? Cosmetics.OutfitCode==Cosmetics.Pack(Cosmetics.BundleOutfit(i)) : Cosmetics.Outfit[category]==i;
                int price=category==5 ? Cosmetics.BundlePrice(i) : Cosmetics.ItemPrice((WardrobeSlot)category,i);
                thumbnail.color=owned?Color.white:new Color(.48f,.50f,.49f,1f);
                Ownership(pick,owned,equipped,item==i);
                Label(pick.transform,"Item status " + i,equipped ? "EQUIPPED" : owned ? "Owned" : "Locked - " + price + " coins",9f,118f,column-18f,20f,15,equipped?Theme.Gold:Theme.Muted);
            }
            bool have=category==5 ? Cosmetics.OwnsCostume(item) : Cosmetics.OwnsItem((WardrobeSlot)category,item);
            int cost=category==5 ? Cosmetics.BundlePrice(item) : Cosmetics.ItemPrice((WardrobeSlot)category,item);
            bool wearing=category==5 ? Cosmetics.OutfitCode==Cosmetics.Pack(shown) : Cosmetics.Outfit[category]==item;
            var buy=Button(body,"Item Action",have ? wearing ? "Equipped" : category==5 ? "Equip bundle" : "Equip item" : "Buy - " + cost + " coins",432f,532f,width-432f,()=>{
                if(!have) {
                    bool bought=category==5 ? Cosmetics.BuyCostume(item) : Cosmetics.BuyItem((WardrobeSlot)category,item);
                    notice=bought ? "Purchased. Equip it whenever you like." : "Not enough coins.";
                } else {
                    if(category==5) Cosmetics.EquipCostume(item); else Cosmetics.EquipItem((WardrobeSlot)category,item);
                    draft=Cosmetics.Outfit; notice="Outfit equipped.";
                }
                changed?.Invoke(); Render();
            },true,have ? GameIcon.Costume : GameIcon.Coin);
            buy.interactable=have ? !wearing : Cosmetics.Coins>=cost;
            Label(body,"Outfit hint",category==5 ? "15% off pieces you do not own." : "Mix individual pieces to create your outfit.",432f,596f,width-432f,30f,18,Theme.Muted);
            Button(body,"Preview emote","Strike a pose",0f,554f,400f,()=>preview.Pose(),false,GameIcon.Laugh,50f);
        }

        void PowersView(float width)
        {
            for(int i=0;i<Powers.All.Count;i++)
            {
                var value=Powers.All[i];
                var select=Button(body,"Power Select " + value,"",i*width/6f,0f,width/6f-10f,()=>{power=value;effect=Cosmetics.Effect(power);Render();},power==value,null,62f);
                var icon=UIKit.CreateImage(select.transform,"Power Icon",SpriteFactory.PowerEmblem(value),Color.white);
                icon.rectTransform.Place(UIKit.Left,new Vector2(32f,0f),new Vector2(48f,48f));
                Label(select.transform,"Name",PowerText.Name(value),62f,0f,width/6f-76f,62f,18,power==value?Theme.TextDark:Theme.Text);
            }
            preview=ScenePreview.Power(Stage(0f,84f,730f,398f),power,effect);
            Button(body,"Replay effect","Replay",0f,492f,180f,()=>preview.Pose(),false,GameIcon.Restart,48f);
            Button(body,"Preview camera","Change view",192f,492f,230f,()=>preview.ToggleCamera(),false,GameIcon.MataElang,48f);
            float x=758f, available=width-x;
            for(int i=0;i<3;i++)
            {
                int value=i;
                bool have=Cosmetics.OwnsEffect(power,i), equipped=Cosmetics.Effect(power)==i;
                var option=Button(body,"Effect " + i,Cosmetics.EffectNames[i],x,84f+i*92f,available,()=>{effect=value;Render();},false,null);
                option.GetComponentInChildren<Text>().rectTransform.offsetMax=new Vector2(-38f,-4f);
                Ownership(option,have,equipped,effect==i);
                Label(body,"Effect status " + i,equipped ? "EQUIPPED" : have ? "Owned" : "Locked - "+Cosmetics.EffectPrices[i]+" coins",x+8f,144f+i*92f,available-16f,26f,18,equipped?Theme.Gold:Theme.Muted);
            }
            bool owned=Cosmetics.OwnsEffect(power,effect);
            var buy=Button(body,"Effect Action",owned ? Cosmetics.Effect(power)==effect ? "Equipped" : "Equip effect" : "Buy - " + Cosmetics.EffectPrices[effect]+" coins",x,398f,available,()=>{
                if(!owned) notice=Cosmetics.BuyEffect(power,effect)?"Purchased. Equip this effect to use it.":"Not enough coins.";
                else {Cosmetics.EquipEffect(power,effect);notice="Effect equipped for " + PowerText.Name(power) + ".";}
                changed?.Invoke();Render();
            },true,owned?GameIcon.Effects:GameIcon.Coin);
            buy.interactable=owned ? Cosmetics.Effect(power)!=effect : Cosmetics.Coins>=Cosmetics.EffectPrices[effect];
            Label(body,"Power context",power==PowerType.Tameng ? "Protects your play from challenges. Sample impact shown." :
                power==PowerType.Cermin ? "Mirror intercepts the shot in front of you and reflects it toward the challenger." :
                "The equipped effect plays at the table when you use this power.",0f,556f,width,64f,21,Theme.Muted);
        }
    }
}

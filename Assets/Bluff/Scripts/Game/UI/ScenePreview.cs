using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Bluff.Core;

namespace Bluff.Game
{
    public sealed class ScenePreview : MonoBehaviour, IDragHandler, IScrollHandler
    {
        GameObject stage;
        Transform model;
        Humanoid actor, opponent;
        readonly List<Card3D> cards = new List<Card3D>();
        TablePowerEffect activeEffect;
        float poseUntil;
        bool effectStarted;
        int poseIndex;
        Camera camera;
        RenderTexture texture;
        RawImage image;
        float yaw = -18f;
        float zoom = 1.15f;
        float nextFrame;
        float cycle;
        bool table;
        bool overview;
        PowerType power;
        int variant;
        const int Layer = 30;
        static int stageIndex;
        public bool IsCharacter => !table;
        public float ViewYaw => yaw;
        public float ViewZoom => zoom;

        public static ScenePreview Character(RectTransform parent, int outfit)
        {
            var preview = Create(parent);
            preview.actor = Humanoid.Create(preview.stage.transform, "Fitting model", Cosmetics.OutfitStyle(outfit), false);
            preview.actor.PreviewAnimation = true;
            preview.actor.SetPortraitPose();
            preview.model = preview.actor.transform;
            foreach(int piece in Cosmetics.Unpack(outfit)) preview.poseIndex+=piece;
            Art.SmoothCylinder("Fitting platform",preview.stage.transform,new Vector3(0f,-.025f,0f),new Vector3(.9f,.02f,.9f),Art.Wood(Theme.FeltRim));
            preview.Pose();
            preview.Prepare();
            return preview;
        }

        public static ScenePreview Power(RectTransform parent, PowerType power, int variant)
        {
            var preview = Create(parent);
            preview.table = true; preview.power = power; preview.variant = variant;
            float top = TableScene.FeltTop;
            Art.SmoothCylinder("Preview table", preview.stage.transform, new Vector3(0f,top-.06f,0f), new Vector3(2f,.06f,2f), Art.Wood(Theme.FeltRim));
            Art.SmoothCylinder("Brass table trim",preview.stage.transform,new Vector3(0f,top+.001f,0f),new Vector3(1.87f,.003f,1.87f),Art.Metal(Theme.Gold,.5f));
            Art.SmoothCylinder("Preview felt", preview.stage.transform, new Vector3(0f,top+.006f,0f), new Vector3(1.84f,.005f,1.84f), Art.Fabric(Theme.Felt));
            Art.SmoothCylinder("Table pedestal",preview.stage.transform,new Vector3(0f,top*.5f,0f),new Vector3(.30f,top*.5f,.30f),Art.Wood(Theme.FeltRim));
            preview.actor = preview.SeatedActor("Power owner",new Vector3(0f,0f,-1.22f),0f,Cosmetics.OutfitStyle(Cosmetics.OutfitCode));
            preview.opponent = preview.SeatedActor("Opponent",new Vector3(1.22f,0f,0f),-90f,HumanoidStyle.ForPlayer(2));
            for (int i = 0; i < 3; i++)
            {
                var card = Card3D.Create(preview.stage.transform, "Preview card");
                card.SetHiddenCard();
                preview.cards.Add(card);
            }
            preview.zoom = .98f;
            preview.Pose();
            preview.Prepare();
            return preview;
        }

        Humanoid SeatedActor(string name, Vector3 position, float rotation, HumanoidStyle style)
        {
            var person=Humanoid.Create(stage.transform,name,style,true);
            person.transform.localPosition=position; person.transform.localRotation=Quaternion.Euler(0f,rotation,0f);
            person.PreviewAnimation=true; person.SetHeldCardCount(6); person.HoldCards();
            var fan=new List<Card3D>();
            for(int i=0;i<6;i++) { var card=Card3D.Create(person.CardHold,"Held card");card.SetHiddenCard();fan.Add(card); }
            TableScene.LayoutFan(fan,false);
            var chair=new GameObject("Chair").transform;chair.SetParent(stage.transform,false);
            chair.localPosition=position;chair.localRotation=person.transform.localRotation;
            Art.BeveledBox("Seat",chair,new Vector3(0f,.45f,-.05f),new Vector3(.48f,.08f,.46f),.025f,Art.Leather(Theme.PanelLight));
            Art.BeveledBox("Chair back",chair,new Vector3(0f,.91f,-.27f),new Vector3(.49f,.84f,.07f),.025f,Art.Wood(Theme.FeltRim));
            person.LookAt(stage.transform.position+Vector3.up*1.1f);
            return person;
        }

        static ScenePreview Create(RectTransform parent)
        {
            var rect = UIKit.CreateRect("Interactive 3D preview", parent);
            rect.Stretch();
            var preview = rect.gameObject.AddComponent<ScenePreview>();
            preview.image = rect.gameObject.AddComponent<RawImage>();
            preview.image.raycastTarget = true;
            preview.stage = new GameObject("Preview stage");
            preview.stage.transform.position = new Vector3(1000f + (stageIndex++ % 100) * 12f,-1000f,1000f);
            var cameraObject = new GameObject("Preview camera", typeof(Camera));
            cameraObject.transform.SetParent(preview.stage.transform,false);
            preview.camera = cameraObject.GetComponent<Camera>();
            preview.camera.enabled = false;
            preview.camera.orthographic = true;
            preview.camera.nearClipPlane = .05f;
            preview.camera.farClipPlane = 8f;
            preview.camera.cullingMask = 1 << Layer;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Theme.Background;
            preview.texture = new RenderTexture(640,640,24,RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            preview.texture.Create();
            preview.camera.targetTexture = preview.texture;
            preview.image.texture = preview.texture;
            preview.Light(new Vector3(-1f,2.4f,2f),Theme.Hex("FFE4B8"),2.1f);
            preview.Light(new Vector3(1.4f,1.8f,1f),Theme.Hex("BDD6DA"),1.4f);
            return preview;
        }

        void Light(Vector3 position, Color color, float intensity)
        {
            var go = new GameObject("Studio light",typeof(Light));
            go.transform.SetParent(stage.transform,false); go.transform.localPosition = position;
            var light = go.GetComponent<Light>();
            light.type = LightType.Point; light.range = 5f; light.intensity = intensity;
            light.color = color; light.cullingMask = 1 << Layer;
        }

        void Prepare()
        {
            foreach (var t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var c in stage.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var light in stage.GetComponentsInChildren<Light>(true)) light.cullingMask = 1 << Layer;
        }

        Vector3 Pile => stage.transform.position+new Vector3(-.08f,TableScene.FeltTop+.025f,.12f);

        void PlayEffect()
        {
            if(power==PowerType.Cermin)
                activeEffect=TablePowerEffect.Mirror(stage.transform,actor.Head.position,opponent.Head.position,opponent.Muzzle.position,variant,stage.transform.position.y+TableScene.FeltTop,true);
            else
                activeEffect=TablePowerEffect.Play(stage.transform,power==PowerType.Hadiah ? actor.CardHold.position : Pile,
                    power,variant,opponent.CardHold.position,false,true);
            Prepare();
        }

        public void Pose()
        {
            if(table)
            {
                cycle=0f; effectStarted=false;
                if(activeEffect!=null) Destroy(activeEffect.gameObject);
                opponent.HoldCards(); actor.HoldCards();
                for(int i=0;i<cards.Count;i++)
                {
                    var card=cards[i];card.gameObject.SetActive(true);card.transform.localScale=Vector3.one;
                    if(power==PowerType.MataElang) card.SetCard(new Card(i,(Element)(i%6))); else card.SetHiddenCard();
                }
                if(power==PowerType.Cermin) opponent.Aim(actor.Head.position);
                else StartCoroutine(actor.PlayPacket(Pile,null,null,1f));
            }
            else
            {
                actor.SetArmMode(poseIndex++%2==0 ? Humanoid.ArmMode.Wave : Humanoid.ArmMode.Celebrate);
                poseUntil=Time.unscaledTime+1.15f;
            }
        }

        void AnimateTable()
        {
            cycle+=Time.unscaledDeltaTime;
            if(cycle>4.6f) {Pose();return;}
            if(cycle>.85f && !effectStarted) {effectStarted=true;PlayEffect();}
            for(int i=0;i<cards.Count;i++)
            {
                var card=cards[i];
                var rest=Pile+new Vector3(i*.013f,i*.004f,0f);
                float deal=Mathf.SmoothStep(0f,1f,(cycle-.23f-i*.07f)/.6f);
                card.transform.position=Vector3.Lerp(actor.CardHold.position,rest,deal)+Vector3.up*Mathf.Sin(deal*Mathf.PI)*.12f;
                card.transform.rotation=Quaternion.Slerp(actor.CardHold.rotation,Quaternion.Euler(90f,i*8f,0f),deal);
                float resolve=Mathf.Clamp01((cycle-1.15f-i*.08f)/1.0f);
                if(power==PowerType.Badai)
                {
                    float angle=resolve*Mathf.PI*5f+i*2f;
                    card.transform.position=rest+new Vector3(Mathf.Cos(angle)*resolve*.22f,resolve*.65f,Mathf.Sin(angle)*resolve*.22f);
                    card.transform.rotation*=Quaternion.Euler(resolve*170f,resolve*330f,0f);
                    card.transform.localScale=Vector3.one*(1f-resolve);
                }
                else if(power==PowerType.Bakar) card.transform.localScale=Vector3.one*(1f-resolve);
                else if(power==PowerType.Hadiah)
                {
                    card.transform.position=Vector3.Lerp(actor.CardHold.position,opponent.CardHold.position,resolve)+Vector3.up*Mathf.Sin(resolve*Mathf.PI)*.32f;
                    card.transform.rotation=Quaternion.Slerp(actor.CardHold.rotation,opponent.CardHold.rotation,resolve);
                    card.gameObject.SetActive(resolve<1f);
                }
                else if(power==PowerType.MataElang)
                {
                    float peek=Mathf.Sin(resolve*Mathf.PI)*.92f;
                    card.transform.rotation=Quaternion.Euler(90f-peek*180f,i*8f,0f);
                    card.transform.position=rest+Vector3.up*peek*.13f;
                }
            }
            if(power==PowerType.Cermin && cycle>2.3f) opponent.HoldCards();
        }

        public void Rotate(float degrees) { yaw += degrees; }
        public void SetView(float angle,float size) {yaw=angle;zoom=size;}
        public void ToggleCamera() {overview=!overview;}
        public void ResetView() { yaw = -18f; zoom = table ? .98f : 1.15f; }
        public void OnDrag(PointerEventData e) { if (!table) yaw -= e.delta.x * .5f; }
        public void OnScroll(PointerEventData e) { if (!table) zoom = Mathf.Clamp(zoom - e.scrollDelta.y * .06f,.65f,1.3f); }

        void LateUpdate()
        {
            if (camera == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            if (table) AnimateTable();
            else if(poseUntil>0f && Time.unscaledTime>poseUntil) {actor.SetArmMode(Humanoid.ArmMode.Rest);poseUntil=0f;}
            if (Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + 1f/30f;
            if (model != null) model.localRotation = Quaternion.Euler(0f,yaw,0f);
            var rect = ((RectTransform)transform).rect;
            camera.aspect = Mathf.Max(.3f, rect.width / Mathf.Max(1f,rect.height));
            camera.orthographic = !table || overview;
            camera.fieldOfView = 56f;
            camera.orthographicSize = zoom;
            var focus = stage.transform.position + Vector3.up * .98f;
            camera.transform.position = focus + (table ? new Vector3(2.0f,1.5f,3.0f) : new Vector3(0f,.04f,3f));
            if(table && !overview)
            {
                focus=stage.transform.position+new Vector3(0f,1.03f,-.30f);
                camera.transform.position=stage.transform.position+new Vector3(-.15f,1.48f,1.48f);
            }
            camera.transform.LookAt(focus);
            // Isolate the stage while rendering; restore scene lights immediately afterwards.
            var masks = new Dictionary<Light,int>();
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (!light.transform.IsChildOf(stage.transform)) { masks[light] = light.cullingMask; light.cullingMask &= ~(1 << Layer); }
            try { camera.Render(); }
            finally { foreach (var entry in masks) if (entry.Key != null) entry.Key.cullingMask = entry.Value; }
        }

        void OnDisable() { if (stage != null) stage.SetActive(false); }
        void OnEnable() { if (stage != null) stage.SetActive(true); }
        void OnDestroy()
        {
            if (image != null) image.texture = null;
            if (camera != null) camera.targetTexture = null;
            if (stage != null) Destroy(stage);
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}

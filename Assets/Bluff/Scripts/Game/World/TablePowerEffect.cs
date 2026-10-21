using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;

namespace Bluff.Game
{
    public sealed class TablePowerEffect : MonoBehaviour
    {
        readonly List<LineRenderer> lines = new List<LineRenderer>();
        readonly Vector3[] points = new Vector3[49];
        PowerType power;
        int variant;
        float age;
        float impact = -10f;
        bool hold;
        bool preview;
        Vector3 destination;
        Vector3 incoming;
        Light glow;
        Color tint;
        Mesh barrierMesh;
        Material barrierMaterial;
        readonly List<Transform> fragments = new List<Transform>();
        Material fragmentMaterial;
        static Texture2D moteTexture, flameTexture;

        public static TablePowerEffect Play(Transform parent, Vector3 origin, PowerType power, int variant,
            Vector3 destination, bool hold = false, bool preview = false)
        {
            var go = new GameObject("Table power " + power);
            go.transform.SetParent(parent,false); go.transform.position = origin;
            var effect = go.AddComponent<TablePowerEffect>();
            effect.power = power; effect.variant = variant; effect.hold = hold; effect.preview = preview;
            effect.destination = destination - origin;
            effect.tint = PowerFlourish.Tint(variant,power);
            for (int i = 0; i < 18; i++)
            {
                var line = new GameObject("Energy path " + i, typeof(LineRenderer)).GetComponent<LineRenderer>();
                line.transform.SetParent(go.transform,false); line.useWorldSpace = false;
                line.sharedMaterial = Art.Additive(Color.white);
                line.widthMultiplier = i < 3 ? .007f : .0035f;
                line.numCornerVertices = 3; line.numCapVertices = 3;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                effect.lines.Add(line);
            }
            var lamp = new GameObject("Reflected glow", typeof(Light)); lamp.transform.SetParent(go.transform,false);
            lamp.transform.localPosition = Vector3.up * .22f;
            effect.glow = lamp.GetComponent<Light>(); effect.glow.type = LightType.Point;
            effect.glow.range = 1.6f; effect.glow.color = effect.tint;
            if (power == PowerType.Tameng) effect.BuildBarrier();
            effect.BuildParticles();
            if(power==PowerType.Cermin) effect.BuildFragments();
            effect.Draw(0f);
            Sfx.Power(go,power,variant,preview);
            return effect;
        }

        public void Impact() { impact = age; }
        public void Dismiss() { hold = false; age = 2.2f; }

        public static TablePowerEffect Mirror(Transform parent, Vector3 protectedHead, Vector3 attackerHead,
            Vector3 muzzle, int variant, float tableSurface, bool preview = false)
        {
            var normal = Vector3.ProjectOnPlane(attackerHead - protectedHead, Vector3.up).normalized;
            if (normal.sqrMagnitude < .1f) normal = Vector3.forward;
            // The entire mirror lies beyond the arms, perpendicular to the incoming shot.
            var center = protectedHead + normal * .68f - Vector3.up * .10f;
            // Classic shards reach 0.445 m below the center at full expansion.
            center.y=Mathf.Max(center.y,tableSurface+.49f);
            var effect = Play(parent, center - Vector3.up * .26f, PowerType.Cermin, variant, attackerHead, false, preview);
            effect.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
            effect.destination = effect.transform.InverseTransformPoint(attackerHead - normal * .24f);
            effect.incoming = effect.transform.InverseTransformPoint(muzzle);
            effect.Draw(0f);
            return effect;
        }

        static Texture2D ParticleTexture(bool flame)
        {
            var existing=flame?flameTexture:moteTexture;
            if(existing!=null) return existing;
            const int size=32;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false) {name=flame?"Soft flame":"Soft mote",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size*2f-1f,v=(y+.5f)/size;
                float width=flame?Mathf.Lerp(.75f,.08f,v):1f;
                float distance=u*u/(width*width)+(v*2f-1f)*(v*2f-1f);
                float alpha=Mathf.Pow(Mathf.Clamp01(1f-distance),2f);
                pixels[y*size+x]=new Color(1f,1f,1f,alpha);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);
            if(flame) flameTexture=texture; else moteTexture=texture;
            return texture;
        }

        void BuildParticles()
        {
            if(power==PowerType.Hadiah || power==PowerType.MataElang) return;
            var go=new GameObject("Power particles");go.transform.SetParent(transform,false);
            var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;
            main.loop=false;main.duration=1.9f;main.startLifetime=new ParticleSystem.MinMaxCurve(.45f,1.1f);
            main.startSize=new ParticleSystem.MinMaxCurve(power==PowerType.Bakar?.07f:.009f,power==PowerType.Bakar?.18f:.023f);
            main.startSpeed=.05f;main.maxParticles=96;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.useUnscaledTime=preview;main.startColor=tint.WithAlpha(.75f);
            main.simulationSpeed=preview?1f:GameSettings.Speed;
            var emission=particles.emission;emission.rateOverTime=power==PowerType.Bakar?38f:22f;
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.16f;
            shape.scale=new Vector3(1f,.06f,1f);shape.position=Vector3.up*.025f;
            if(power==PowerType.Cermin) {shape.scale=new Vector3(1f,1f,.03f);shape.position=Vector3.up*.26f;}
            var velocity=particles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;
            velocity.y=new ParticleSystem.MinMaxCurve(.18f,.5f);
            velocity.x=velocity.z=new ParticleSystem.MinMaxCurve(0f,0f);
            velocity.orbitalY=new ParticleSystem.MinMaxCurve(variant==1?-.8f:variant==2?2.5f:.6f);
            var colors=particles.colorOverLifetime;colors.enabled=true;
            colors.color=new Gradient {colorKeys=new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(tint,1f)},
                alphaKeys=new[]{new GradientAlphaKey(0f,0f),new GradientAlphaKey(.9f,.15f),new GradientAlphaKey(0f,1f)}};
            var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1f,AnimationCurve.EaseInOut(0f,1f,1f,0f));
            var renderer=particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=Art.Additive(Color.white,ParticleTexture(power==PowerType.Bakar));
            renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            particles.Play();
        }

        void BuildFragments()
        {
            fragmentMaterial=Art.AdditiveInstance(tint.WithAlpha(.22f));
            for(int i=0;i<8;i++)
            {
                var piece=Art.Primitive(PrimitiveType.Quad,"Mirror facet",transform,Vector3.zero,new Vector3(.08f,.11f,1f),fragmentMaterial);
                fragments.Add(piece.transform);
            }
        }

        void BuildBarrier()
        {
            const int columns = 48, rows = 16;
            var vertices = new Vector3[(columns+1)*(rows+1)];
            var uv = new Vector2[vertices.Length];
            var indices = new int[columns*rows*6];
            float radius = .25f + (variant == 1 ? .025f : 0f);
            for (int row=0;row<=rows;row++)
                for (int col=0;col<=columns;col++)
                {
                    float a=col/(float)columns*Mathf.PI*2f, h=row/(float)rows*Mathf.PI*.5f;
                    int index=row*(columns+1)+col;
                    float section=variant==1 ? PolygonRadius(a,6) : variant==2 ? 1f+.12f*Mathf.Cos(a*6f)*Mathf.Sin(h*2f) : 1f;
                    vertices[index]=new Vector3(Mathf.Cos(a)*Mathf.Cos(h)*radius*section,Mathf.Sin(h)*.38f,Mathf.Sin(a)*Mathf.Cos(h)*radius*section);
                    uv[index]=new Vector2(col/(float)columns,row/(float)rows);
                    if(row==rows || col==columns) continue;
                    int offset=(row*columns+col)*6;
                    indices[offset]=index;indices[offset+1]=index+columns+1;indices[offset+2]=index+1;
                    indices[offset+3]=index+1;indices[offset+4]=index+columns+1;indices[offset+5]=index+columns+2;
                }
            barrierMesh=new Mesh { name="Shield surface",vertices=vertices,uv=uv,triangles=indices };
            barrierMesh.RecalculateNormals();barrierMesh.RecalculateBounds();
            barrierMaterial=new Material(Resources.Load<Shader>("BluffShaders/BluffBarrier"));
            var shell=new GameObject("Shield membrane",typeof(MeshFilter),typeof(MeshRenderer));
            shell.transform.SetParent(transform,false);shell.GetComponent<MeshFilter>().sharedMesh=barrierMesh;
            var renderer=shell.GetComponent<MeshRenderer>();renderer.sharedMaterial=barrierMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void OnDestroy()
        {
            if(barrierMesh!=null) Destroy(barrierMesh);
            if(barrierMaterial!=null) Destroy(barrierMaterial);
            if(fragmentMaterial!=null) Destroy(fragmentMaterial);
        }

        void Update()
        {
            age += (preview ? Time.unscaledDeltaTime : Time.deltaTime * GameSettings.Speed);
            if (!hold && age >= 2.8f) { Destroy(gameObject); return; }
            if (preview && power == PowerType.Tameng && age > 1.15f && impact < 0f) Impact();
            Draw(age);
        }

        void Draw(float t)
        {
            float fade = Mathf.Clamp01(t * 5f) * (hold ? 1f : Mathf.Clamp01((2.8f-t)*2f));
            float burst = Mathf.Clamp01(1f - (t-impact)*3f);
            float radius = .25f + (variant == 1 ? .025f : 0f);
            glow.intensity = fade * (.35f + burst * 1.2f);
            if(barrierMaterial!=null) barrierMaterial.color=Color.Lerp(tint,Color.white,burst*.65f).WithAlpha(fade*(1f+burst));
            if(fragmentMaterial!=null) fragmentMaterial.color=Color.Lerp(tint,Color.white,.4f).WithAlpha(fade*.28f);
            for(int i=0;i<fragments.Count;i++)
            {
                float a=i*Mathf.PI*.25f+t*(variant==2?1.6f:.35f);
                float r=variant==1?.23f:.18f+Mathf.Sin(t*2f+i)*.025f;
                fragments[i].localPosition=new Vector3(Mathf.Cos(a)*r,.26f+Mathf.Sin(a)*r,Mathf.Sin(t*2f+i)*.035f);
                fragments[i].localRotation=Quaternion.Euler(0f,Mathf.Sin(t*3f+i)*35f,a*Mathf.Rad2Deg+45f);
            }
            for (int i = 0; i < lines.Count; i++)
            {
                float alpha = fade * (i < 3 ? .8f : .45f);
                if (power == PowerType.Tameng && i == 16) alpha *= preview && t > .8f && t < 1.2f ? 1f : 0f;
                if (power == PowerType.Tameng && i == 17) alpha *= burst;
                if (power == PowerType.Badai && i >= 8) alpha *= .25f;
                if (power == PowerType.Bakar && i >= 2) alpha *= Mathf.Sin((t*.65f+i*.071f)%1f*Mathf.PI);
                if (power == PowerType.MataElang && i >= 4) alpha *= i == 4 ? .7f : 0f;
                if (power == PowerType.MataElang && variant > 0 && i >= 4) alpha = fade*.5f;
                if (power == PowerType.Cermin && i >= 16) alpha *= t>.65f && t<1.9f ? 1f : 0f;
                for (int j = 0; j < points.Length; j++)
                {
                    float u = j / 48f;
                    float a = u * Mathf.PI * 2f;
                    float spin = t * (variant == 2 ? 2.1f : .7f);
                    if (i < 2 && power != PowerType.Cermin)
                    {
                        float r = radius * (i == 0 ? 1.12f : 1.35f);
                        if (power == PowerType.Badai) r *= 1f + .15f*Mathf.Sin(t*5f);
                        if (variant==1) r*=PolygonRadius(a+spin*.2f,i==0?6:12);
                        if (variant==2) {r*=1f+.12f*Mathf.Sin(a*6f-spin*2f);a+=spin*.3f;}
                        points[j] = new Vector3(Mathf.Cos(a)*r,.009f+i*.004f,Mathf.Sin(a)*r);
                        continue;
                    }
                    switch (power)
                    {
                        case PowerType.Tameng:
                            if (i < 10)
                            {
                                float elevation = (i-2)/8f * Mathf.PI*.5f;
                                float ring=variant==1?PolygonRadius(a,6):1f;
                                if(variant==2) {elevation=((i-2)/8f+t*.16f)%1f*Mathf.PI*.5f;ring+=.12f*Mathf.Cos(a*6f)*Mathf.Sin(elevation*2f);}
                                points[j] = new Vector3(Mathf.Cos(a)*Mathf.Cos(elevation)*radius*ring, Mathf.Sin(elevation)*.38f,Mathf.Sin(a)*Mathf.Cos(elevation)*radius*ring);
                            }
                            else if (i < 16)
                            {
                                float b = (i-10)*Mathf.PI/3f + (variant==0 ? spin*.12f : variant==2 ? Mathf.Sin(u*Mathf.PI)*.3f : 0f);
                                points[j] = new Vector3(Mathf.Cos(b)*Mathf.Cos(u*Mathf.PI*.5f)*radius, Mathf.Sin(u*Mathf.PI*.5f)*.38f,Mathf.Sin(b)*Mathf.Cos(u*Mathf.PI*.5f)*radius);
                            }
                            else
                            {
                                float travel = Mathf.Clamp01((t-0.8f)*3f);
                                var hit = new Vector3(-radius*.72f,.25f,0f);
                                if (i == 16) points[j] = Vector3.Lerp(new Vector3(-.7f,.42f,0f), hit,travel-u*.08f);
                                else { float r = Mathf.Clamp01((t-impact)*2f)*.2f; points[j] = hit + new Vector3(0f,Mathf.Cos(a)*r,Mathf.Sin(a)*r); }
                            }
                            break;
                        case PowerType.Badai:
                            float h = u*.7f;
                            float spiral = a*2f + spin*5f + i*.5f;
                            float funnel = .08f + u*.28f;
                            points[j] = new Vector3(Mathf.Cos(spiral)*funnel,h,Mathf.Sin(spiral)*funnel);
                            if(variant==1)
                            {
                                float level=(i-2)/16f;
                                float ringA=a+t*(i%2==0?2f:-2f);
                                float r=(.1f+level*.3f)*PolygonRadius(ringA,6);
                                points[j]=new Vector3(Mathf.Cos(ringA)*r,level*.62f+Mathf.Sin(t*4f+level*6f)*.025f,Mathf.Sin(ringA)*r);
                            }
                            else if(variant==2)
                            {
                                float sweep=a*1.2f-t*5f+i*2.4f;
                                float r=.12f+.15f*Mathf.Sin(u*Mathf.PI);
                                points[j]=new Vector3(Mathf.Cos(sweep)*r,u*.7f,Mathf.Sin(sweep)*r)+new Vector3(Mathf.Sin(u*6f+t)*.11f,0f,0f);
                            }
                            break;
                        case PowerType.Bakar:
                            float flame = (t*.65f+i*.071f)%1f;
                            float angle = i*2.4f;
                            float rise = flame + u*.10f;
                            points[j] = new Vector3(Mathf.Cos(angle+rise*2f)*.17f*(1f-rise),rise*.55f,Mathf.Sin(angle+rise*2f)*.17f*(1f-rise));
                            if(variant==1)
                            {
                                float grow=Mathf.Clamp01(t*2f);
                                float r=.19f*(1f-u)*grow;
                                points[j]=new Vector3(Mathf.Cos(angle)*r,u*.43f+Mathf.Sin(u*Mathf.PI)*.055f,Mathf.Sin(angle)*r);
                            }
                            else if(variant==2)
                            {
                                float r=.18f*(1f-u);
                                float curl=angle+u*6f-t*4f;
                                points[j]=new Vector3(Mathf.Cos(curl)*r,u*.62f+Mathf.Sin(t*5f+i)*.018f,Mathf.Sin(curl)*r);
                            }
                            break;
                        case PowerType.MataElang:
                            if (i == 2) points[j] = new Vector3(Mathf.Cos(a)*.22f,.19f+Mathf.Sin(a)*.09f,.02f);
                            else if (i == 3) points[j] = new Vector3(Mathf.Cos(a)*.047f,.19f+Mathf.Sin(a)*.047f,.025f);
                            else { float sweep = (t*.5f)%1f; points[j] = new Vector3((u-.5f)*.43f,.025f+i*.0003f,(sweep-.5f)*.46f); }
                            if(variant==1 && i>=4)
                            {
                                float spoke=(i-4)*Mathf.PI/7f+t*.25f;
                                float r=.10f+u*.19f;
                                points[j]=new Vector3(Mathf.Cos(spoke)*r,.018f+Mathf.Sin(u*Mathf.PI)*.025f,Mathf.Sin(spoke)*r);
                            }
                            else if(variant==2 && i>=4)
                            {
                                float wing=i<11?-1f:1f, feather=(i-4)%7;
                                points[j]=new Vector3(wing*(.06f+u*(.18f+feather*.013f)),.19f+Mathf.Sin(u*Mathf.PI)*(.08f+feather*.013f),Mathf.Sin(t*2f+u*3f)*.018f);
                            }
                            break;
                        case PowerType.Cermin:
                            if(i<2)
                            {
                                float frame=.29f+i*.028f;
                                if(variant==1) frame*=PolygonRadius(a,6);
                                points[j]=new Vector3(Mathf.Cos(a)*frame,.26f+Mathf.Sin(a)*frame,0f);
                                break;
                            }
                            float shard = (i-2)*Mathf.PI/8f + spin*.3f;
                            float expansion = Mathf.Clamp01((t-.5f)*2f)*.16f;
                            var center = new Vector3(Mathf.Cos(shard)*(.21f+expansion),.25f+Mathf.Sin(shard)*(.21f+expansion),0f);
                            float corner = u*Mathf.PI*2f;
                            points[j] = center + new Vector3(Mathf.Cos(corner)*.045f,Mathf.Sin(corner)*.075f,Mathf.Sin(t*3f+i)*.03f);
                            if(variant==1)
                            {
                                float r=(i<9?.20f:.28f)*PolygonRadius(a+t*.35f,6);
                                points[j]=new Vector3(Mathf.Cos(a+t*.35f)*r,.26f+Mathf.Sin(a+t*.35f)*r,(i%7)*.007f);
                            }
                            else if(variant==2)
                            {
                                float ripple=((t*.5f+i*.07f)%1f)*.35f;
                                points[j]=new Vector3(Mathf.Cos(a)*ripple,.26f+Mathf.Sin(a)*ripple,Mathf.Sin(a*4f+t*3f)*.018f);
                            }
                            if(i>=16)
                            {
                                var hit=Vector3.up*.26f;
                                float shot=Mathf.Clamp01((t-.65f)/.55f-u*.12f);
                                points[j]=i==16?Vector3.Lerp(incoming,hit,shot):Vector3.Lerp(hit,destination,Mathf.Clamp01((t-1.2f)/.55f-u*.12f));
                            }
                            break;
                        default:
                            float path = Mathf.Clamp01((t*.52f + i*.035f)%1f - u*.09f);
                            points[j] = Vector3.Lerp(Vector3.zero,destination,path) + Vector3.up*Mathf.Sin(path*Mathf.PI)*.32f
                                + new Vector3(Mathf.Cos(i*1.7f),0f,Mathf.Sin(i*1.7f))*.018f;
                            if(variant==1)
                            {
                                float travel=Mathf.Clamp01((t*.6f+(i-2)*.035f)%1f);
                                var along=Vector3.Lerp(Vector3.zero,destination,travel)+Vector3.up*Mathf.Sin(travel*Mathf.PI)*.32f;
                                float diamond=PolygonRadius(a,4)*.037f;
                                points[j]=along+new Vector3(Mathf.Cos(a)*diamond,Mathf.Sin(a)*diamond,0f);
                            }
                            else if(variant==2)
                            {
                                var side=Vector3.Cross(destination.normalized,Vector3.up).normalized;
                                float helix=path*Mathf.PI*6f+i*Mathf.PI;
                                points[j]+=side*Mathf.Cos(helix)*.045f+Vector3.up*Mathf.Sin(helix)*.045f;
                            }
                            break;
                    }
                }
                lines[i].positionCount = points.Length; lines[i].SetPositions(points);
                lines[i].startColor = tint.WithAlpha(alpha); lines[i].endColor = Color.Lerp(tint,Color.white,.3f).WithAlpha(alpha*.6f);
            }
        }

        static float PolygonRadius(float angle,int sides)
        {
            float sector=Mathf.PI*2f/sides;
            return Mathf.Cos(Mathf.PI/sides)/Mathf.Cos(Mathf.Repeat(angle+sector*.5f,sector)-sector*.5f);
        }
    }
}

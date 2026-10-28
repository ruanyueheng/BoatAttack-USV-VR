Shader "BoatAttack/FishingPortal"
{
    Properties
    {
        _Tint("Sea-glass light",Color)=(.42,.84,.91,1)
        _Shoulder("Arch shoulder",Float)=.69
        _Height("Portal height",Float)=3.08
        _Mode("Stardust mode",Float)=0
        _PulseTime("Interaction pulse",Float)=-10
        _Motion("Ambient motion",Range(0,1))=1
        _ZWrite("Depth write",Float)=1
        _Theme("Island theme 0 wind 1 grove 2 tide 3 star",Float)=3
        _Accent("Accent light",Color)=(1,.86,.52,1)
        [NoScaleOffset]_NoiseTex("Value-noise lattice",2D)="gray"{}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            // The near-opaque veil writes depth so screen-space haze reads the doorway, not the scene behind it.
            Blend SrcAlpha OneMinusSrcAlpha ZWrite [_ZWrite] Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_NoiseTex);SAMPLER(sampler_NoiseTex);
            float _FishingPortalLowEnd; // global, set by FishingMobileQuality on PICO/Android: one layer per effect
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint,_Accent;
            float _Shoulder,_Height,_Mode,_PulseTime,_Motion,_Theme;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; float2 seed:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; half twinkle:TEXCOORD2; float seed:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A a)
            {
                V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);
                if(_Mode>.5)
                {
                    a.p.y+=sin(_Time.y*.23+a.seed.x*6.28)*.09*_Motion;
                    a.p.x+=sin(_Time.y*.19+a.seed.y*6.28)*.045*_Motion;
                    a.p.z+=cos(_Time.y*.17+a.seed.x*6.28)*.035*_Motion;
                }
                v.p=TransformObjectToHClip(a.p.xyz);v.uv=a.uv;v.fog=ComputeFogFactor(v.p.z);v.seed=a.seed.x;
                v.twinkle=lerp(.48,.36+.44*smoothstep(-.8,.8,sin(_Time.y*.55+a.seed.y*6.28)),_Motion);return v;
            }
            // Lattice lookups replace sin-hashes; LOD 0 keeps them valid inside loops and branches.
            float Hash(float2 p){return SAMPLE_TEXTURE2D_LOD(_NoiseTex,sampler_NoiseTex,(floor(p)+.5+frac(p)*.37)*.015625,0).r;}
            // 64x64 random lattice, bilinear + repeat: one fetch replaces four sin-hashes per call (PICO ALU budget).
            float Noise(float2 p){return SAMPLE_TEXTURE2D_LOD(_NoiseTex,sampler_NoiseTex,(p+.5)*.015625,0).r;}
            // One twinkling four-point star per cell; a cheap stand-in for a star texture.
            half StarField(float2 p,float t,float density)
            {
                float2 cell=floor(p),f=frac(p)-.5;float h=Hash(cell);
                float2 o=(float2(Hash(cell+7.1),Hash(cell+3.3))-.5)*.6;float2 d=abs(f-o);
                half star=(exp(-dot(d,d)*900)*1.6+saturate(.004/(d.x*d.y*40+.002)-.15)*exp(-dot(d,d)*60)*.6)*step(1-density,h);
                return star*(.55+.45*sin(t*(1.3+h*2.1)+h*40));
            }
            half4 Frag(V v):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                if(_Mode>.5)
                {
                    float2 q=abs(v.uv*2-1);
                    half core=exp(-dot(q,q)*10),rays=pow(saturate(1-q.x*7)*saturate(1-q.y),3)+pow(saturate(1-q.y*7)*saturate(1-q.x),3);
                    half halo=exp(-dot(q,q)*3)*.22;
                    half density=lerp(step(frac(v.seed),.26),1,_Motion);
                    return half4(MixFog(lerp(_Tint.rgb,_Accent.rgb,.85)*1.25,v.fog),saturate(core*.7+rays*.8+halo)*v.twinkle*density);
                }
                float2 q=float2((v.uv.x-.5)*2,max(0,(v.uv.y-_Shoulder)/(1-_Shoulder)));
                float t=_Time.y*_Motion;
                // Each island shapes its own rim: wind flutters, grove roots roughen, tide rolls, star ripples softly.
                float wob=_Theme<.5?sin(v.uv.y*34-t*3.2+q.x*6)*.016:_Theme<1.5?(Noise(float2(v.uv.y*14,q.x*3))-.5)*.07*smoothstep(.45,0,v.uv.y):_Theme<2.5?sin(v.uv.y*12-t*1.1)*.022:sin(v.uv.y*28-t*1.6+q.x*5)*.012;
                float edge=min(1-abs(q.x),1-dot(q,q))+wob*_Motion;clip(edge);
                float3 origin=float3(unity_ObjectToWorld._m03,unity_ObjectToWorld._m13,unity_ObjectToWorld._m23);
                half near=1-smoothstep(1.2,6,distance(_WorldSpaceCameraPos.xyz,origin));
                float2 p=(v.uv-float2(.5,.47))*float2(1,_Height*.5);
                float r=length(p),a=atan2(p.y,p.x);
                half3 accent=_Accent.rgb,color;
                // Linear-space palettes; uniform branch on a per-material constant, so one shader and no extra draw calls.
                if(_Theme<.5)
                {
                    // Wind: pale cyan-white sky with ribbons of air streaming around the door.
                    float swirl=a+r*1.6-t*.5;
                    float2 w=float2(p.x+t*.35,p.y+sin(p.x*2.5+t*.8)*.18+Noise(p*2+t*.2)*.25);
                    half ribbons=pow(.5+.5*sin(w.y*13+sin(w.x*3)*1.2),10)+pow(.5+.5*sin(w.y*7-w.x*2.2+swirl),14)*.7;
                    color=lerp(half3(.05,.20,.30),half3(.28,.55,.66),saturate(v.uv.y*.7+Noise(w*3)*.3))+half3(.85,.97,1)*ribbons*.75;
                    color+=accent*StarField(float2(w.x*6,p.y*9),t*1.8,.18)*.9;
                }
                else if(_Theme<1.5)
                {
                    // Grove: looking up through a canopy; dark crown, warm gold-green light deepening toward the centre.
                    half glow=exp(-dot(p-float2(0,.15),p-float2(0,.15))*2.6);
                    // Target #123524 -> #2E6B3A with #F3D9A0 light; base pushed cooler to survive the warm daytime grade.
                    half3 sun=half3(.896,.693,.352);
                    color=lerp(half3(.004,.032,.016),half3(.024,.140,.040),saturate(glow*.9))+sun*exp(-dot(p-float2(0,.35),p-float2(0,.35))*22)*.6;
                    half shafts=pow(saturate(.5+.5*sin((p.x*.9+p.y*.45)*4.2+t*.15)),10)*saturate(.3+v.uv.y*.7);
                    color+=lerp(half3(.10,.25,.08),sun,.5)*shafts*.12*(.5+glow);
                    // Two parallax layers of swaying leaves (one pointed ellipse per cell), denser toward the crown.
                    [unroll] for(int k=0;k<2;k++)
                    {
                        if(k>0 && _FishingPortalLowEnd>.5)break;
                        float sway=sin(t*(.5+k*.2)+p.y*1.5+k*2)*(.03+k*.04);
                        float2 m=(p+float2(sway,0))*(4.5+k*3.5)+k*11.3;float2 cell=floor(m);float h=Hash(cell+k*5);
                        float ang=h*6.28+sin(t*.6+h*9)*.15*_Motion;float2 f=frac(m)-.5-(float2(Hash(cell+1.7),Hash(cell+8.3))-.5)*.3;
                        f=float2(f.x*cos(ang)-f.y*sin(ang),f.x*sin(ang)+f.y*cos(ang));
                        float len=.32+.16*Hash(cell+4.4);float ld=abs(f.x)-len*.42*(1-pow(f.y/len,2));half leaf=smoothstep(.01+k*.03,0,ld)*step(abs(f.y),len);half lit=smoothstep(.035,0,abs(ld+.012))*step(abs(f.y),len);
                        leaf*=step(lerp(.85,.15,saturate(v.uv.y*1.1+.1-k*.15)),Hash(cell+2.2));
                        half lt=lit*step(lerp(.85,.15,saturate(v.uv.y*1.1+.1-k*.15)),Hash(cell+2.2));
                        // Back-lit leaves: translucent green toward the light, near-black emerald at the crown; far layer softer for depth.
                        half3 tone=lerp(half3(.002,.016,.008),half3(.06,.22,.04),glow*(k==0?.9:.6)*(.6+.4*Hash(cell+6.6)));
                        color=lerp(color,tone,leaf*(k==0?.75:.95))+sun*lt*glow*(k==0?.2:.5);
                    }
                    float2 sp=p*20+float2(sin(t*.3)*.5,t*.25);
                    color+=half3(1,.78,.72)*StarField(sp,t,.16)*.6*smoothstep(.7,0,v.uv.y)+accent*StarField(p*14+3,t,.12)*.35;
                }
                else if(_Theme<2.5)
                {
                    // Tide: a standing sea surface lit from below, with layered caustics and drifting bubbles.
                    color=lerp(half3(.004,.05,.07),half3(.001,.012,.03),saturate(v.uv.y*.9));
                    half rays=pow(saturate(.5+.5*sin(p.x*5+sin(p.y*1.3+t*.3)*.8)),6)*smoothstep(1.1,-.2,v.uv.y);
                    color+=half3(.10,.42,.40)*rays*.55;
                    [unroll] for(int k=0;k<2;k++)
                    {
                        if(k>0 && _FishingPortalLowEnd>.5)break;
                        float sc=k==0?1.6:3.4;float2 c=p*sc+float2(Noise(p*2+t*.2+k*7),Noise(p*2-t*.17+k*3))*1.1;
                        half caustic=pow(saturate(1-abs(Noise(c*1.7+t*.15)-Noise(c*1.7-t*.12+4))*(k==0?6:10)),5);
                        color+=half3(.30,.80,.75)*caustic*(k==0?.28:.18)*(.4+.6*smoothstep(1,0,v.uv.y));
                    }
                    // Random-size bubbles; each column rises at its own speed and wobbles.
                    [unroll] for(int k=0;k<2;k++)
                    {
                        if(k>0 && _FishingPortalLowEnd>.5)break;
                        float sc=k==0?5:9;float col=floor(p.x*sc+k*.5);float hc=Hash(float2(col,k));
                        float2 b=float2(p.x*sc+k*.5,p.y*sc-t*(.8+1.6*hc)+hc*9);float2 cell=floor(b);float h=Hash(cell+k*17);
                        float2 f=frac(b)-.5-float2((h-.5)*.5+sin(t*2+h*20)*.06,(Hash(cell+3.3)-.5)*.4);
                        float size=.06+.16*Hash(cell+9.1);float l=length(f);
                        half bubble=(smoothstep(size,size*.7,l)*.25+exp(-pow((l-size*.85)*60/sc*5,2))*.6+exp(-dot(f+size*.35,f+size*.35)*900)*.8)*step(.68,h);
                        color+=half3(.65,.95,1)*bubble*(k==0?.8:.5);
                    }
                }
                else
                {
                    // Star: night-blue nebula vortex with gold constellations.
                    float swirl=a+r*4.2-t*(.35+near*.35);
                    half arms=pow(.5+.5*cos(swirl*2),3)*smoothstep(1.3,.1,r);
                    float2 flow=float2(cos(swirl),sin(swirl))*r;
                    half cloud=Noise(flow*3+t*.05)*.6+Noise(flow*7-t*.08)*.4;
                    half detail=Noise(flow*16+float2(t*.11,0))*.5+Noise(flow*31-t*.07)*.5;
                    half3 deep=half3(.011,.024,.091),mist=half3(.159,.238,.578);
                    half band=saturate(arms*(.55+cloud*.7)*(.75+detail*.5));
                    color=lerp(deep,mist,band*band*.9)+mist*smoothstep(.55,.95,band*detail)*.35;
                    float spin=-t*.12;float2 sp=float2(p.x*cos(spin)-p.y*sin(spin),p.x*sin(spin)+p.y*cos(spin));
                    color+=accent*(StarField(sp*10,t,.4)+StarField(sp*19+5,t*1.4,.3)*.6+StarField(flow*14,t,.25)*band)*(1+near*.4);
                    // Constellation lines between bright stars in neighbouring cells.
                    float2 g=sp*3.2,cg=floor(g);float2 s0=cg+.5+(float2(Hash(cg+7.1),Hash(cg+3.3))-.5)*.6;half lines=0;
                    for(int k=0;k<2;k++)
                    {
                        if(k>0 && _FishingPortalLowEnd>.5)break;
                        float2 n=cg+(k==0?float2(1,0):float2(0,1));float2 s1=n+.5+(float2(Hash(n+7.1),Hash(n+3.3))-.5)*.6;
                        float2 e=s1-s0,d=g-s0;float u=saturate(dot(d,e)/dot(e,e));
                        lines+=exp(-pow(length(d-e*u)*90,2))*step(.55,Hash(cg+k*13.7));
                    }
                    color+=accent*lines*.18;
                    color=lerp(color,accent,exp(-r*r*12)*(.38+near*.1)*(.9+.1*sin(t*1.7)));
                }
                color=lerp(color,_Tint.rgb*_Tint.rgb,_Theme>.5 && _Theme<1.5?0:.04);
                // Thin self-lit rim echoing the arch inlay; a soft glow keeps the veil from reading as a hole in sunlight.
                half rim=_Theme>1.5 && _Theme<2.5?exp(-pow((edge-.04)*22,2))*.6:exp(-pow((edge-.035)*45,2));
                color+=accent*rim*(1.1+near*.4)*(.85+.15*sin(v.uv.y*40+t*2))+(_Theme>.5 && _Theme<1.5?0:accent*.025+accent*accent*near*.04);
                float age=_Time.y-_PulseTime;
                half pulse=(age>=0 && age<1.6)?(1-age/1.6):0;
                half wave=pulse*exp(-pow((r-age*1.4)*5,2));
                color+=accent*(wave*.5+pulse*pulse*.08)*_Motion;
                half alpha=smoothstep(0,.012,edge)*smoothstep(0,.012,v.uv.y);
                // Fog only tints the veil so the scene inside survives hazy daytime weather.
                return half4(lerp(color,MixFog(color,v.fog),_Theme>.5 && _Theme<1.5?.08:.3),saturate(alpha));
            }
            ENDHLSL
        }
    }
}

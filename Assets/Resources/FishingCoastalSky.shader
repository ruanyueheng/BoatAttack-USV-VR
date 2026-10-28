Shader "BoatAttack/FishingCoastalSky"
{
    Properties
    {
        _SkyTint("Sky tint",Color)=(.5,.5,.5,1) _Daylight("Daylight",Float)=1 _CoastTime("Cloud time",Float)=0 _SunDirection("Sun direction",Vector)=(0,1,0,0)
        _Storm("Storm cover",Range(0,1))=0 _CloudCover("Cloud cover",Range(0,1))=.18 _Lightning("Distant lightning",Range(0,1))=0 _LightningDirection("Lightning direction",Vector)=(0,.28,1,0)
        [NoScaleOffset]_NoiseTex("Tileable cloud noise (R broad, G detail)",2D)="gray"{} [NoScaleOffset]_CumulusTex("Baked cumulus atlas (R big, G small, B erosion)",2D)="black"{}
        _MoonDirection("Moon direction",Vector)=(0,.5,.8,0) _MoonPhase("Moon phase 0 new .5 full",Range(0,1))=.4 _StarVisibility("Night sky visibility",Range(0,1))=0
        _Meteor("Meteor head xyz, progress w (<0 none)",Vector)=(0,1,0,-1) _MeteorDir("Meteor travel",Vector)=(1,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _SkyTint,_SunDirection,_LightningDirection,_MoonDirection,_Meteor,_MeteorDir;
            float _Daylight,_CoastTime,_Storm,_CloudCover,_Lightning,_MoonPhase,_StarVisibility;
            float4x4 _StarRotation;
            float4 _CardDirs[12];
            float _FishingSkyLowEnd,_FishingPortalLowEnd; // globals: PICO/Android or reduced effects
            TEXTURE2D(_NoiseTex);SAMPLER(sampler_NoiseTex);TEXTURE2D(_CumulusTex);SAMPLER(sampler_CumulusTex);
            struct A { float4 p:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A a){V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);v.p=TransformObjectToHClip(a.p.xyz);v.direction=a.p.xyz;return v;}
            float3 Hash33(float3 p){p=frac(p*float3(.1031,.1030,.0973));p+=dot(p,p.yxz+33.33);return frac((p.xxy+p.yxx)*p.zyx);}
            float Noise(float2 uv,int channel){float4 n=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,uv);return channel==0?n.r:n.g;}
            // Little Prince dusk palette #fedd9d #efba72 #c38858 #985674 #503067, linearised; t=0 horizon glow, 1 upper violet.
            float3 Dusk(float t)
            {
                t=saturate(t)*4;
                float3 c=lerp(float3(.991,.723,.341),float3(.863,.491,.168),saturate(t));
                c=lerp(c,float3(.539,.246,.098),saturate(t-1));c=lerp(c,float3(.314,.093,.177),saturate(t-2));
                return lerp(c,float3(.080,.029,.141),saturate(t-3));
            }
            half4 Frag(V v):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                float3 d=normalize(v.direction);float h=saturate(d.y);float day=saturate(_Daylight);
                float3 sun=normalize(_SunDirection.xyz);float sunDot=dot(d,sun);
                float coverage=saturate(_CloudCover),storm=saturate(_Storm);

                float3 top=lerp(float3(.004,.010,.034),float3(.030,.17,.62),day);
                float3 horizon=lerp(float3(.018,.036,.085),float3(.36,.60,.88),day);
                float3 sky=lerp(horizon,top,pow(h,.45));
                // Twilight: sun within ~12 degrees of the horizon paints a sunward warm band and a violet zenith.
                float twilight=saturate(1-abs(sun.y-.03)/.24)*(1-storm*.85);
                // Civil dusk/dawn (sun just below the horizon): grey-violet wash before the blue night.
                float violet=saturate(1-abs(sun.y+.16)/.15)*(1-storm*.85);
                float2 dh=normalize(d.xz+float2(1e-5,0)),sh=normalize(sun.xz+float2(1e-5,0));float sunward=dot(dh,sh)*.5+.5;
                // Gaps between sunset clouds stay pale lavender rather than deep violet.
                float3 dusk=lerp(Dusk(min(h*2.3+(1-sunward)*.55,.7)),float3(.58,.44,.66),saturate(h*1.8)*(1-sunward*.5));
                sky=lerp(sky,dusk,twilight*(.45+.5*sunward*sunward));
                sky=lerp(sky,lerp(float3(.20,.12,.26),float3(.07,.06,.17),h),violet*.8);
                sky+=Dusk(0)*pow(saturate(sunDot),10)*twilight*.45;

                // Baked cumulus atlas (1024px): two parallax decks, one sun-offset tap each for lit edges.
                float2 cp=d.xz/(max(d.y,0)+.12);
                float2 uvA=cp*.06+_CoastTime*float2(.0006,.0002),uvB=cp*.16+_CoastTime*float2(.0019,.0006)+.5;
                float3 a=SAMPLE_TEXTURE2D(_CumulusTex,sampler_CumulusTex,uvA).rgb;
                float b=SAMPLE_TEXTURE2D(_CumulusTex,sampler_CumulusTex,uvB).g;
                float n=max(a.r,b*.4);
                float2 toSun=sh*.010;
                float toward=max(SAMPLE_TEXTURE2D(_CumulusTex,sampler_CumulusTex,uvA+toSun).r,SAMPLE_TEXTURE2D(_CumulusTex,sampler_CumulusTex,uvB+toSun*1.6).g*.4);
                float cover=saturate(coverage+twilight*.85+violet*.3);
                float threshold=lerp(.52,.06,cover);
                // Soft wispy edge eroded by fine noise; distant clouds thin out toward the horizon.
                float cloud=smoothstep(threshold,threshold+.18,n+(Noise(cp*1.3+_CoastTime*float2(.002,0),1)-.5)*.22)*lerp(.35,1,smoothstep(.02,.30,d.y))*smoothstep(.0,.05,d.y);
                float thick=saturate((n-threshold)*1.8);
                float lit=saturate(.62+(n-toward)*6-thick*.30);
                float cirrus=smoothstep(.56,.82,Noise(cp*float2(.05,.32)+_CoastTime*float2(.0007,0)+.71,1))*smoothstep(.06,.35,d.y)*(1-coverage)*.30;
                float3 litCloud=lerp(float3(.010,.013,.032),float3(1.0,.94,.80),day);
                float3 shadeCloud=lerp(float3(.003,.005,.013),float3(.52,.60,.76),day);
                float3 coral=lerp(float3(.93,.40,.52),float3(.99,.62,.46),sunward*sunward);
                litCloud=lerp(litCloud,coral,twilight);
                shadeCloud=lerp(shadeCloud,lerp(float3(.45,.20,.36),float3(.70,.32,.30),sunward*sunward),twilight);
                litCloud=lerp(litCloud,float3(.30,.24,.38),violet*.8);shadeCloud=lerp(shadeCloud,float3(.12,.09,.18),violet*.8);
                float3 cloudColor=lerp(shadeCloud,litCloud,lit);
                // Sunset rim: thin sunward edges glow #efba72 -> #c38858.
                float rim=saturate((n-toward)*14)*(1-thick)*twilight*(.4+.6*sunward);
                cloudColor=lerp(cloudColor,lerp(float3(.539,.246,.098),float3(.863,.491,.168),sunward)*1.6,rim);
                cloudColor=lerp(cloudColor,lerp(float3(.30,.34,.40),float3(.72,.75,.78),day)*(.4+.6*day),coverage*coverage*.55);
                // Night clouds are near-black except a thin moonlit rim.
                cloudColor+=float3(.09,.10,.14)*pow(saturate(dot(d,normalize(_MoonDirection.xyz))),24)*saturate((n-toward)*8+.2)*_StarVisibility;
                // Card fade costs 12 cone tests per sky pixel: desktop only.
                float near=0;if(max(_FishingSkyLowEnd,_FishingPortalLowEnd)<.5)for(int k=0;k<12;k++)near=max(near,smoothstep(_CardDirs[k].w*1.4,_CardDirs[k].w*.4,acos(saturate(dot(d,_CardDirs[k].xyz))))*step(.001,_CardDirs[k].w));
                cloud*=1-near*.75;
                float sky0=cloud*lerp(.97,.85,1-thick);
                sky=lerp(sky,lerp(sky,litCloud,.7),cirrus);
                sky=lerp(sky,cloudColor,sky0);

                // Night: stars and the milky way rotate about the pole (matrix from game clock).
                if(_StarVisibility>.001)
                {
                    float3 s=mul((float3x3)_StarRotation,d);
                    float g=dot(s,float3(.42,.35,.837));
                    float mwNoise=Noise(float2(atan2(s.z,s.x)*.45,g*1.8),0);
                    float dust=smoothstep(.45,.75,Noise(float2(atan2(s.z,s.x)*.9,g*4),1))*exp(-g*g*260);
                    float band=exp(-g*g*22)*(.45+mwNoise)*(1-dust*.65);
                    float3 night=float3(.17,.17,.25)*band;
                    float3 p=s*120,cell=floor(p),r=Hash33(cell);
                    float bright=smoothstep(.86-band*.08,1,r.x);
                    float dist=length(p-cell-(.2+.6*Hash33(cell+7.1)));
                    float twinkle=.62+.38*sin(_Time.y*(1.7+r.z*3.3)+r.y*40);
                    float3 tint=lerp(float3(1,.86,.70),float3(.76,.86,1),r.z);
                    night+=tint*bright*bright*(1-smoothstep(.05,.19,dist))*lerp(1,twinkle,smoothstep(.0,.35,h)*.8+.2)*(1.4+band*2);
                    if(_Meteor.w>=0)
                    {
                        float3 head=normalize(_Meteor.xyz);float3 m=d-head;float along=dot(m,-_MeteorDir.xyz);
                        float perp=length(m+_MeteorDir.xyz*along);
                        float fade=sin(saturate(_Meteor.w)*3.14159);
                        night+=float3(1,.95,.85)*fade*step(0,along)*(1-saturate(along/.11))*(1-smoothstep(.0004,.0018,perp))*2.2;
                    }
                    sky+=night*_StarVisibility*saturate((-.105-sun.y)/.1)*(1-sky0)*(1-cirrus)*smoothstep(.0,.10,d.y);
                }

                // Moon: phase-lit disc with maria from the same noise; a faint daytime moon in clear weather.
                float3 moon=normalize(_MoonDirection.xyz);float moonDot=dot(d,moon);
                float moonVisible=saturate(_StarVisibility+day*.22*(1-coverage))*(1-storm);
                float full=1-abs(_MoonPhase*2-1);
                sky+=float3(.55,.62,.80)*pow(saturate(moonDot),900)*.18*full*_StarVisibility*(1-sky0*.6);
                if(moonDot>.998 && moonVisible>.001)
                {
                    float3 right=normalize(cross(float3(0,1,0),moon)),up=cross(moon,right);
                    float2 q=float2(dot(d,right),dot(d,up))/.03;float r2=dot(q,q);
                    float3 nrm=float3(q,sqrt(saturate(1-r2)));
                    float phase=_MoonPhase*6.28318;
                    float lit=smoothstep(-.05,.12,dot(nrm,float3(sin(phase),0,-cos(phase))));
                    float maria=saturate(.78+.75*(Noise(q*.22+.5,0)-.5)+.35*(Noise(q*.5+.2,1)-.5));
                    float3 moonColor=float3(1,.95,.84)*(maria*lit*.9+.03);
                    float disc=1-smoothstep(.90,1,r2);
                    sky=lerp(sky,lerp(sky+moonColor*.5,moonColor,1-day*.6),disc*moonVisible*(1-sky0*.85));
                }

                // Sun disc and halo, hidden by thick cloud.
                float sunDisc=smoothstep(.99982,.99992,sunDot)*step(-.01,d.y);
                float3 sunColor=lerp(float3(5,2.4,1.0),float3(5,4.6,3.9),saturate(sun.y*5));
                sky+=sunColor*sunDisc*(1-sky0*.92)*(1-storm);
                float halo=pow(saturate(sunDot),96);
                sky+=float3(1,.72,.37)*halo*.32*day*(1-storm)*(1-coverage*.8);

                if(storm>.001)
                {
                    // Broad continuous overcast replaces the blue hemisphere during rain.
                    float2 heavyP=d.xz/(max(d.y,0)+.23)*.12+float2(_CoastTime*.0006,-_CoastTime*.00025);
                    float heavy=Noise(heavyP,0)*.7+Noise(heavyP*2.3,1)*.3;
                    float under=smoothstep(.32,.76,heavy);
                    float3 stormTop=lerp(float3(.018,.025,.045),float3(.105,.135,.18),day);
                    float3 stormHorizon=lerp(float3(.035,.045,.065),float3(.30,.35,.39),day);
                    float3 overcast=lerp(stormHorizon,stormTop,pow(h,.38))*lerp(.65,1.18,under);
                    overcast+=float3(.035,.039,.045)*smoothstep(.35,.73,Noise(heavyP*3.1+float2(.37,.81),1))*(.25+.75*day);
                    sky=lerp(sky,overcast,storm);
                    if(_Lightning>.001)
                    {
                        float3 centre=normalize(_LightningDirection.xyz);
                        float3 right=normalize(cross(float3(0,1,0),centre));
                        float3 up=cross(centre,right);
                        float2 q=float2(dot(d,right),dot(d,up));
                        float y=saturate((q.y+.13)/.27);
                        float zig=(sin(y*23)+sin(y*57)*.35)*.014;
                        float trunk=1-smoothstep(.001,.003,abs(q.x-zig));
                        trunk*=step(-.13,q.y)*step(q.y,.14);
                        float fork=1-smoothstep(.0007,.0022,abs(q.x-zig-(.04-q.y)*.42));
                        fork*=step(-.055,q.y)*step(q.y,.04);
                        float bolt=saturate(trunk+fork*.6)*step(.8,dot(d,centre));
                        sky+=float3(.22,.28,.36)*_Lightning*storm*(.4+under*.6);
                        sky+=float3(.85,.91,1)*bolt*_Lightning*3*storm;
                    }
                }
                sky*=lerp(float3(.8,.9,1.1),.9+min(_SkyTint.rgb*2,1)*.1,day);
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}

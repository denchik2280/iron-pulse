Shader "IronPulse/PixelSurface"
{
    Properties
    {
        _BaseColor("Color", Color) = (.4,.4,.4,1)
        _Emission("Emission", Float) = 0
        _Metallic("Metallic", Range(0,1)) = 0
        _Roughness("Roughness", Range(.08,1)) = .72
        _Grain("Fine surface grain", Range(0,.3)) = .045
        _TextureScale("Texels per metre", Float) = 96
        _SurfaceKind("Surface: solid / metal / walnut / skin / cloth", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode"="DepthForwardOnly" }
            Cull [_Cull] ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            struct DA {float3 p:POSITION;};
            struct DV {float4 p:SV_POSITION;};
            DV VertDepth(DA i){DV o;o.p=TransformWorldToHClip(TransformObjectToWorld(i.p));return o;}
            float4 FragDepth(DV i):SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Cull [_Cull] ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Emission, _Metallic, _Roughness, _Grain;
                float _TextureScale, _SurfaceKind, _Cull;
            CBUFFER_END
            struct A {float3 p:POSITION; float3 n:NORMAL;};
            struct V {float4 p:SV_POSITION; float3 wp:TEXCOORD0; float3 n:TEXCOORD1; float3 mp:TEXCOORD2;};
            V Vert(A i)
            {
                V o;
                o.wp=TransformObjectToWorld(i.p);
                o.p=TransformWorldToHClip(o.wp);
                o.n=TransformObjectToWorldNormal(i.n);
                // Object-anchored, metre-scaled texture coordinates: moving hands and weapons
                // keep their finish while large world meshes keep an even texel density.
                float3x3 m=(float3x3)GetObjectToWorldMatrix();
                float3 s=float3(length(mul(m,float3(1,0,0))),length(mul(m,float3(0,1,0))),length(mul(m,float3(0,0,1))));
                o.mp=i.p*s;
                return o;
            }
            float Hash(float3 p)
            {
                p=frac(p*.1031);
                p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            float SoftNoise(float3 p)
            {
                float3 cell=floor(p),f=frac(p);
                f=f*f*(3-2*f);
                float n00=lerp(Hash(cell),Hash(cell+float3(1,0,0)),f.x);
                float n10=lerp(Hash(cell+float3(0,1,0)),Hash(cell+float3(1,1,0)),f.x);
                float n01=lerp(Hash(cell+float3(0,0,1)),Hash(cell+float3(1,0,1)),f.x);
                float n11=lerp(Hash(cell+float3(0,1,1)),Hash(cell+float3(1,1,1)),f.x);
                return lerp(lerp(n00,n10,f.y),lerp(n01,n11,f.y),f.z);
            }
            float3 Env(float3 r)
            {
                float up=saturate(r.y*.5+.5);
                float3 sky=lerp(float3(.105,.115,.135),float3(.43,.51,.61),up);
                float strip=pow(saturate(dot(r,normalize(float3(-.45,.85,-.35)))),24);
                return sky+float3(.72,.64,.50)*strip;
            }
            float4 Frag(V i):SV_Target
            {
                float3 n=normalize(i.n);
                float3 view=normalize(GetCameraPositionWS()-i.wp);
                float3 key=normalize(float3(-.45,.85,-.35));
                float3 fill=normalize(float3(.7,.35,.5));
                float nl=saturate(dot(n,key));
                float nv=max(.035,saturate(dot(n,view)));
                float metal=saturate(_Metallic);
                float rough=clamp(_Roughness,.08,1);
                float texelScale=max(_TextureScale,1);
                float3 texel=i.mp*texelScale;
                // Fade microscopic detail before it aliases. The remaining restrained texel
                // pattern carries the pixel-realism style without a low-resolution image.
                float footprint=max(length(ddx(texel)),length(ddy(texel)));
                float fineFade=saturate(1-footprint*.6);
                float grain=(Hash(floor(texel))-.5)*_Grain*fineFade;
                float mottling=(SoftNoise(i.mp*3.6)-.5)*_Grain*.65;
                float3 albedo=max(_BaseColor.rgb,0)*(1+grain+mottling);
                if(_SurfaceKind>1.5 && _SurfaceKind<2.5)
                {
                    // Oiled walnut: elongated, irregular grain follows each mesh's long axis.
                    float curl=SoftNoise(i.mp*float3(10,8,2))*3.7;
                    float rings=sin(i.mp.x*155+i.mp.y*34+curl)*.5+.5;
                    float pores=SoftNoise(i.mp*float3(170,100,7));
                    float wood=(rings-.5)*.13+(pores-.5)*.11;
                    albedo*=1+wood*fineFade;
                    albedo*=lerp(float3(.86,.77,.65),float3(1.08,1.04,.94),rings);
                }
                if(_SurfaceKind>3.5)
                {
                    float2 weave=floor(i.mp.xy*texelScale);
                    float thread=fmod(weave.x+weave.y,2)*2-1;
                    albedo*=1+thread*.028*fineFade;
                }
                float hemi=n.y*.5+.5;
                float3 ambient=lerp(float3(.30,.30,.31),float3(.57,.64,.72),hemi);
                float3 diffuse=ambient+nl*float3(.57,.53,.47)+saturate(dot(n,fill))*.11*float3(.56,.70,.84);
                // Metals retain their dark oxide colour under a broad environment reflection;
                // rough stone and skin retain a soft, readable diffuse response.
                float3 f0=lerp(float3(.035,.035,.035),albedo,metal);
                float3 h=normalize(key+view);
                float nh=saturate(dot(n,h));
                float specPower=lerp(150,5,rough*rough);
                float spec=pow(nh,specPower)*nl*lerp(2.2,.28,rough);
                float fresnel=pow(1-nv,5);
                float3 reflection=Env(reflect(-view,n));
                float3 fresnelColor=f0+(1-f0)*fresnel;
                float3 color=albedo*diffuse*lerp(1,.40,metal);
                color+=f0*spec*1.15;
                color+=reflection*fresnelColor*lerp(.12,.76,metal)*(1-rough*.45);
                if(_SurfaceKind>2.5 && _SurfaceKind<3.5)
                {
                    float wrap=saturate((dot(n,key)+.28)/1.28);
                    color+=albedo*float3(.16,.064,.035)*wrap;
                }
                // Emission is deliberately compressed so orange outline and reactor accents
                // stay saturated and silhouettes remain readable against them.
                color+=_BaseColor.rgb*(_Emission/(1+_Emission*.20))*.40;
                float3 absoluteWS=GetAbsolutePositionWS(i.wp);
                float distanceToCamera=distance(GetCameraPositionWS(),i.wp);
                float fog=saturate((distanceToCamera-33)/125);
                float lowHaze=1-saturate(absoluteWS.y/70);
                color=lerp(color,float3(.055,.074,.096),fog*.58*lowHaze);
                return float4(max(color,0),1);
            }
            ENDHLSL
        }
    }
}

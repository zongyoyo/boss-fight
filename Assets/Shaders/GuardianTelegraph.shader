Shader "BossFight/AreaTelegraph"
{
    Properties { _Color("Color", Color)=(1,1,1,0.7) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float3 pos:POSITION; }; struct V { float4 pos:SV_POSITION; float3 world:TEXCOORD0; };
            float4 _Color,_Origin,_Forward,_Field,_Shape,_Size,_Safe0,_Safe1,_Safe2,_Visual;
            V vert(A a) { V o; o.world=TransformObjectToWorld(a.pos); o.pos=TransformWorldToHClip(o.world); return o; }
            half4 frag(V i):SV_Target
            {
                float2 v=i.world.xz-_Origin.xz, f=normalize(_Forward.xz+float2(0,0.000001));
                float x=dot(v,float2(f.y,-f.x)), z=dot(v,f), r=length(v);
                float fieldDistance=_Field.z-distance(i.world.xz,_Field.xy);
                float d=fieldDistance;
                float progress=r/max(_Shape.y,0.001);
                if(_Shape.x<0.5) d=_Shape.y-r;
                else if(_Shape.x<1.5) { d=min(_Shape.y-r,r-_Shape.z); progress=(r-_Shape.z)/max(_Shape.y-_Shape.z,0.001); }
                else if(_Shape.x<2.5)
                {
                    float halfAngle=radians(_Shape.w*0.5), a=atan2(x,z);
                    d=min(_Shape.y-r,(halfAngle-abs(a))*max(r,0.001));
                    progress=(a+halfAngle)/max(halfAngle*2,0.001);
                }
                else if(_Shape.x<3.5) { d=min(min(z,_Size.x-z),_Size.y*0.5-abs(x)); progress=z/max(_Size.x,0.001); }
                else if(_Shape.x<4.5)
                {
                    d=max(_Size.y*0.5-abs(v.x),_Size.y*0.5-abs(v.y));
                    progress=max(abs(v.x),abs(v.y))/max(_Field.z+distance(_Origin.xz,_Field.xy),0.001);
                }
                else progress=distance(i.world.xz,_Field.xy)/max(_Field.z,0.001);
                d=min(d,fieldDistance);
                clip(d);
                float safe=0;
                if(_Size.w>0.5) safe=max(safe,step(distance(i.world.xz,_Safe0.xz),_Field.w));
                if(_Size.w>1.5) safe=max(safe,step(distance(i.world.xz,_Safe1.xz),_Field.w));
                if(_Size.w>2.5) safe=max(safe,step(distance(i.world.xz,_Safe2.xz),_Field.w));
                float filled=step(progress,_Size.z);
                float edge=1-smoothstep(0.04,0.14,d);
                float fillEdge=1-smoothstep(0.005,0.025,abs(progress-_Size.z));
                half4 c=_Color;
                c.rgb*=0.65+filled*0.4+edge*0.65+_Visual.x*0.35;
                c.a*=saturate(0.35+filled*0.35+edge*0.55+fillEdge*0.3+_Visual.x*0.2);
                // 安全區完整顯示綠色，爆發區完整保留紅色底，填充不改輪廓。
                if(safe>0) c=half4(0.2,2,0.6,0.7);
                return c;
            }
            ENDHLSL
        }
    }
}

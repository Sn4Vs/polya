Shader "Gazik/SoftCoat" {
 Properties { _Color("Tint", Color)=(1,1,1,1) _Glossiness("Softness", Range(0,1))=.22 _Fibre("Micro texture",Range(0,1))=.15 _RimStrength("Velvet rim",Range(0,.2))=.035 _EmissionColor("Emission",Color)=(0,0,0,1) }
 SubShader {
  Tags { "RenderType"="Opaque" } LOD 250
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows vertex:vert
  #pragma target 3.0
  #include "UnityCG.cginc"
  struct Input { float4 color:COLOR; float3 worldPos; float3 viewDir; };
  fixed4 _Color,_EmissionColor; half _Glossiness, _Fibre,_RimStrength;
  void vert(inout appdata_full v, out Input o) { UNITY_INITIALIZE_OUTPUT(Input,o); o.color=v.color; }
  void surf(Input i, inout SurfaceOutputStandard o) {
   float n=frac(sin(dot(floor(i.worldPos*520),float3(12.9898,78.233,42.121)))*43758.5453);
   fixed3 coat=_Color.rgb*i.color.rgb*(1+(n-.5)*_Fibre);
   o.Albedo=coat; o.Smoothness=_Glossiness; o.Metallic=0;
   o.Emission=_EmissionColor.rgb+coat*pow(1-saturate(dot(normalize(i.viewDir),float3(0,0,1))),3)*_RimStrength;
   o.Alpha=1;
  }
  ENDCG
 } FallBack "Diffuse"
}

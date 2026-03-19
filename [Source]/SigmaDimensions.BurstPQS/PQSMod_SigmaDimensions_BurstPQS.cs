using BurstPQS;
using Unity.Burst;

namespace PQSMod_SigmaDimensions_BurstPQS
{
    [BurstCompile]
    [BatchPQSMod(typeof(PQSMod_SigmaDimensions.PQSMod_SigmaDimensions))]
    class PQSMod_SigmaDimensions_BurstPQS : BatchPQSMod<PQSMod_SigmaDimensions.PQSMod_SigmaDimensions>
    {
        public PQSMod_SigmaDimensions_BurstPQS(PQSMod_SigmaDimensions.PQSMod_SigmaDimensions mod) : base(mod) { }
        public override void OnQuadPreBuild(PQ quad, BatchPQSJobSet jobSet)
        {
            base.OnQuadPreBuild(quad, jobSet);

            jobSet.Add(new BuildJob
            {
                sphereRadius = Mod.sphere.radius,
                Resize = Mod.Resize,
                landscape = Mod.landscape
            });
        }
        [BurstCompile]
        struct BuildJob : IBatchPQSHeightJob
        {
            public double sphereRadius;
            public double Resize;
            public double landscape;
            public void BuildHeights(in BuildHeightsData data)
            {
                for (int i = 0; i < data.VertexCount; ++i)
                {
                    data.vertHeight[i] = sphereRadius + (data.vertHeight[i] - sphereRadius) * Resize * landscape;
                }
            }
        }
    }
}

﻿using System;
using UnityEngine;
using Kopernicus;

namespace SigmaDimensionsPlugin
{
    public class PQSCityFixer : MonoBehaviour
    {
        double time = 0;

        void Update()
        {
            if (time < 0.2)
            {
                time += Time.deltaTime;
            }
            else
            {
                time = 0;

                CelestialBody body = FlightGlobals.currentMainBody;
                if (body == null)
                {
                    return;
                }

                PQS pqs = body.pqsController;
                if (pqs == null)
                {
                    return;
                }

                PQSCity city = GetComponent<PQSCity>();
                if (city == null) 
                {
                    return;
                }

                // Location
                Vector3 planet = body.transform.position;
                Vector3 building = city.transform.position; // From body to city
                Vector3 location = (building - planet).normalized;

                // Sigma Dimensions Settings
                double resize = body.Has("resize") ? body.Get<double>("resize") : 1;
                double landscape = body.Has("landscape") ? body.Get<double>("landscape") : 1;
                double resizeBuildings = body.Has("resizeBuildings") ? body.Get<double>("resizeBuildings") : 1;

                // Max distance
                double maxDistance = Math.Abs(2 * pqs.mapMaxHeight);
                maxDistance *= resize * landscape > 1 ? resize * landscape : 1;
                maxDistance += body.Radius;

                RaycastHit[] hits = Physics.RaycastAll(planet + location * (float)maxDistance, -location, (float)maxDistance, LayerMask.GetMask("Local Scenery"));

                for (int i = 0; i < hits?.Length; i++)
                {
                    if (hits[i].collider?.GetComponent<PQ>())
                    {
                        Debug.Log("PQSCityFixer", "> Planet: " + body.transform.name);
                        Debug.Log("PQSCityFixer", "    > PQSCity: " + city);

                        // PQSCity parameters
                        double oldGroundLevel = pqs.GetSurfaceHeight(city.repositionRadial) - body.Radius;
                        Debug.Log("PQSCityFixer", "        > Old Ground Level at Mod (GETSURFACE) = " + oldGroundLevel);
                        double oldOceanOffset = body.ocean && oldGroundLevel < 0 ? oldGroundLevel : 0d;
                        Debug.Log("PQSCityFixer", "        > Old Ocean Offset at Mod = " + oldOceanOffset);
                        oldGroundLevel = body.ocean && oldGroundLevel < 0 ? 0d : oldGroundLevel;
                        Debug.Log("PQSCityFixer", "        > Old Ground Level at Mod (WITH OCEAN) = " + oldGroundLevel);

                        double groundLevel = (hits[i].point - planet).magnitude - body.Radius;
                        Debug.Log("PQSCityFixer", "        > Ground Level at Mod (RAYCAST) = " + groundLevel);
                        double oceanOffset = body.ocean && groundLevel < 0 ? groundLevel : 0d;
                        Debug.Log("PQSCityFixer", "        > Ocean Offset at Mod = " + oceanOffset);
                        groundLevel = body.ocean && groundLevel < 0 ? 0d : groundLevel;
                        Debug.Log("PQSCityFixer", "        > Ground Level at Mod (WITH OCEAN) = " + groundLevel);

                        // Fix Altitude
                        if (!city.repositionToSphere && !city.repositionToSphereSurface)
                        {
                            // Offset = Distance from the center of the planet
                            // THIS IS NOT POSSIBLE AS OF KSP 1.7.1

                            Debug.Log("PQSCityFixer", "        > PQSCity Current Center Offset = " + city.repositionRadiusOffset);

                            double builtInOffset = (city.repositionRadiusOffset - body.Radius - oldGroundLevel - oceanOffset) / resizeBuildings - (groundLevel + oceanOffset - oldGroundLevel - oldOceanOffset) / (resize * landscape);

                            Debug.Log("PQSCityFixer", "        > Builtin Offset = " + builtInOffset);

                            city.repositionRadiusOffset = body.Radius + groundLevel + oceanOffset + builtInOffset * resizeBuildings;

                            Debug.Log("PQSCityFixer", "        > PQSCity Fixed Center Offset = " + city.repositionRadiusOffset);
                        }
                        else if (city.repositionToSphere && !city.repositionToSphereSurface)
                        {
                            // Offset = Distance from the radius of the planet

                            Debug.Log("PQSCityFixer", "        > PQSCity Current Radius Offset = " + city.repositionRadiusOffset);

                            double builtInOffset = (city.repositionRadiusOffset - oldGroundLevel) / resizeBuildings - (groundLevel - oldGroundLevel) / (resize * landscape);

                            Debug.Log("PQSCityFixer", "        > Builtin Offset = " + builtInOffset);

                            city.repositionRadiusOffset = groundLevel + builtInOffset * resizeBuildings;

                            Debug.Log("PQSCityFixer", "        > PQSCity Fixed Radius Offset = " + city.repositionRadiusOffset);
                        }
                        else
                        {
                            // Offset = Distance from the surface of the planet

                            Debug.Log("PQSCityFixer", "        > PQSCity Current Surface Offset = " + city.repositionRadiusOffset);

                            double builtInOffset = city.repositionRadiusOffset / resizeBuildings - (groundLevel + oceanOffset - oldGroundLevel - oldOceanOffset) / (resize * landscape);

                            Debug.Log("PQSCityFixer", "        > Builtin Offset = " + builtInOffset);

                            city.repositionRadiusOffset = builtInOffset * resizeBuildings + groundLevel + oceanOffset - oldGroundLevel - oldOceanOffset;

                            Debug.Log("PQSCityFixer", "        > PQSCity Fixed Surface Offset = " + city.repositionRadiusOffset);
                        }

                        // Apply Changes and Destroy
                        city.Orientate();
                        DestroyImmediate(this);
                    }
                }
            }
        }
    }
}

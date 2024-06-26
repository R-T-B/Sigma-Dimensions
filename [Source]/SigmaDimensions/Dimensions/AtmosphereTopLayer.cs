﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Kopernicus;
using Kopernicus.ConfigParser.BuiltinTypeParsers;

namespace SigmaDimensionsPlugin
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class AtmosphereTopLayer : MonoBehaviour
    {
        //from CashnipLeaf: is it necessary to have this as an instance variable?
        //TODO: look into whether this can be deleted.
        Ktype curve = Ktype.Exponential;

        void Start()
        {
            foreach (CelestialBody body in FlightGlobals.Bodies.FindAll(b => b.atmosphere && b.Has("atmoTopLayer")))
            {
                // Debug
                PrintCurve(body, "Original Curves");

                Normalize(body, body.atmosphereDepth);

                double topLayer = body.Get<double>("atmoTopLayer") * body.atmosphereDepth;
                FixPressure(body, topLayer);
                QuickFix(body.atmosphereTemperatureCurve, topLayer);
                QuickFix(body.atmosphereTemperatureSunMultCurve, topLayer);
                body.atmosphereDepth = topLayer; //from CashnipLeaf: was originally FixMaxAltitude(body, topLayer). I inlined it as it was only a single line.

                Normalize(body, 1 / body.atmosphereDepth);

                // Debug
                PrintCurve(body, "Final Curves");
            }
        }

        void FixPressure(CelestialBody body, double topLayer)
        {
            FloatCurve curve = body.atmospherePressureCurve;
            List<double[]> list = ReadCurve(curve);

            //From CashnipLeaf: Hack fix that isnt necessary anymore. I believe this was necessary at some point to prevent issues with calculating ISP and delta-v,
            //but that's now handled by Kopernicus. All this does now is break homeworld atmospheres. I've commented it out to disable it.
            /* Remove ISP FIX   ==> */
            //if (body.transform.name == "Kerbin" && list.Count > 0) { list.RemoveAt(0); } 

            /* Avoid Bad Curves ==> */
            if (list.Count < 2) 
            { 
                UnityEngine.Debug.Log("SigmaLog: This pressure curve has " + (list.Count == 0 ? "no keys" : "just one key") + ". I don't know what you expect me to do with that."); 
                return; 
            }

            double maxAltitude = list.Last()[0];

            bool smoothEnd = list.Last()[1] == 0 && list.Count > 2;

            if (smoothEnd) 
            {
                list.RemoveAt(list.Count - 1);
            }

            if (topLayer > maxAltitude)
            {
                Extend(list, topLayer);
                maxAltitude = list.Last()[0];
            }

            if (topLayer < maxAltitude)
            {
                Trim(list, topLayer);
            }

            if (smoothEnd)
            {
                Smooth(list);
            }

            //From CashnipLeaf: See my earlier comment about the hack fix.
            /* Restore ISP FIX ==> */
            //if (body.transform.name == "Kerbin") { list.Insert(0, new[] { 0, 101.325, 0, 0, }); }

            curve.Load(WriteCurve(list));
        }

        void QuickFix(FloatCurve curve, double topLayer)
        {
            if (topLayer > curve.maxTime)
            {
                List<double[]> list = ReadCurve(curve); 
                /* Avoid Bad Curves ==> */ 
                if (list.Count == 0) 
                { 
                    Debug.Log("AtmosphereTopLayer.QuickFix", "This curve is pointless."); 
                    return; 
                }
                list.Last()[3] = 0;
                list.Add(new double[] { topLayer, list.Last()[1], 0, 0 });
                curve.Load(WriteCurve(list));
            }
        }

        // Editors

        void Extend(List<double[]> list, double topLayer)
        {
            double newAltitude = list.Last()[0];
            double dX = list.Last()[0] - list[list.Count - 2][0];
            double[] K = getK(list);

            for (int i = 0; newAltitude < topLayer; i++)
            {
                newAltitude += dX;
                double newPressure = getY(newAltitude, list.Last(), K);
                double tangent = (newPressure - getY(newAltitude - dX * 0.01, list.Last(), K)) / (dX * 0.01);

                double[] newKey = { newAltitude, newPressure, tangent, tangent };

                if (newKey[1] < 0)
                {
                    if (list.Last()[1] == 0)
                    {
                        break;
                    }
                    else
                    {
                        newKey[1] = 0;
                    }   
                }

                list.Add(newKey);
            }

            // Debug
            PrintCurve(list, "Extend");
        }

        void Trim(List<double[]> list, double topLayer)
        {
            FloatCurve curve = new FloatCurve();
            curve.Load(WriteCurve(list));

            double[] lastKey = { topLayer, curve.Evaluate((float)topLayer) };

            for (int i = list.Count; i > 0; i--)
            {
                if (list[i - 2][0] < topLayer)
                {
                    double dX = 0.01 * (lastKey[0] - list[i - 2][0]);
                    double dY = lastKey[1] - curve.Evaluate((float)(lastKey[0] - dX));
                    double tangent = dY / dX;

                    list.RemoveAt(i - 1);

                    list.Add(new double[] { lastKey[0], lastKey[1], tangent, tangent });
                    break;
                }
                else
                {
                    list.RemoveAt(i - 1);
                }
            }

            // Debug
            PrintCurve(list, "Trim");
        }

        void Smooth(List<double[]> list)
        {
            FloatCurve curve = new FloatCurve();
            curve.Load(WriteCurve(list));
            double minPressure = list.First()[1];
            double maxPressure = list.First()[1];

            for (int i = 0; i < list.Count; i++)
            {
                //from CashnipLeaf: this should make what's going on more obvious
                minPressure = Math.Min(minPressure, list[i][1]);
                maxPressure = Math.Max(maxPressure, list[i][1]);
                /* OLD CODE
                if (list[i][1] < minPressure)
                {
                    minPressure = list[i][1];
                }
                if (list[i][1] > maxPressure)
                {
                    maxPressure = list[i][1];
                }
                */
            }

            for (int i = 0; i < list.Count; i++)
            {
                list[i][1] = (list[i][1] - minPressure) * maxPressure / (maxPressure - minPressure);

                if (i > 0)
                {
                    double dX = 0.01 * (list[i][0] - list[i - 1][0]);
                    double dY = list[i][1] - ((curve.Evaluate((float)(list[i][0] - dX)) - minPressure) * maxPressure / (maxPressure - minPressure));
                    list[i][2] = dY / dX;
                    list[i][3] = dY / dX;
                }
            }

            list.Last()[2] = 0;
            list.Last()[3] = 0;

            // Debug
            PrintCurve(list, "Smooth");
        }

        // Normalizers

        void Normalize(CelestialBody body, double altitude)
        {
            if (body.atmospherePressureCurveIsNormalized)
            {
                Multiply(body.atmospherePressureCurve, altitude);
            }
            if (body.atmosphereTemperatureCurveIsNormalized)
            {
                Multiply(body.atmosphereTemperatureCurve, altitude);
            }
        }

        void Multiply(FloatCurve curve, double multiplier)
        {
            List<double[]> list = new List<double[]>(); 
            list = ReadCurve(curve);
            foreach (double[] key in list)
            {
                key[0] *= multiplier;
                key[2] /= multiplier;
                key[3] /= multiplier;
            }
            curve.Load(WriteCurve(list));
        }

        // Readers

        List<double[]> ReadCurve(FloatCurve curve)
        {
            ConfigNode config = new ConfigNode();
            List<double[]> list = new List<double[]>();
            NumericCollectionParser<double> value = new NumericCollectionParser<double>();

            curve.Save(config);

            foreach (string k in config.GetValues("key"))
            {
                value.SetFromString(k);
                list.Add(value.Value.ToArray());
            }

            return list;
        }

        ConfigNode WriteCurve(List<double[]> list)
        {
            ConfigNode config = new ConfigNode();

            foreach (double[] values in list)
            {
                string key = "";
                foreach (double value in values)
                {
                    key += value + " ";
                }
                config.AddValue("key", key);
            }

            return config;
        }

        // Values

        double[] getK(List<double[]> list)
        {
            //from CashnipLeaf: is it necessary to decare a K if it just gets returned immediately after its set to a value?
            //TODO: remove the K
            double[] K = { };
            if (list.Count == 2)
            {
                // Polynomial Curve:    dY = dX * ( K0 * (X0 + X1) + K1 )
                K = new double[] { 0, (list[1][1] - list[0][1]) / (list[1][0] - list[0][0]) };
                curve = Ktype.Polynomial;
                return K;
            }
            double[] dY = { list[list.Count - 2][1] - list[list.Count - 3][1], list[list.Count - 1][1] - list[list.Count - 2][1] };
            double[] dX = { list[list.Count - 2][0] - list[list.Count - 3][0], list[list.Count - 1][0] - list[list.Count - 2][0] };
            double curvature = (dY[1] / dX[1] - dY[0] / dX[0]) / ((dX[0] + dX[1]) / 2);

            if (curvature > 0)
            {
                // Exponential Curve:   Y1/Y0 = EXP(dX * K);
                K = new double[] { Math.Log(list.Last()[1] / list[list.Count - 2][1]) / dX[1] };
                curve = Ktype.Exponential;
            }
            else if (curvature < 0 && dY[1] >= 0)
            {
                // Logarithmic Curve:   dY = K * LN(X1/X0);
                K = new double[] { dY[1] / Math.Log(list.Last()[0] / list[list.Count - 2][0]) };
                curve = Ktype.Logarithmic;
            }
            else
            {
                // Polynomial Curve:    dY = dX * ( K0 * (X0 + X1) + K1 )
                K = new double[] { curvature / 2, dY[1] / dX[1] - (list.Last()[0] + list[list.Count - 2][0]) * curvature / 2 };
                curve = Ktype.Polynomial;
            }

            return K;
        }

        double getY(double X, double[] prevKey, double[] K)
        {
            double dX = X - prevKey[0];

            //from CashnipLeaf: Converted to a switch statement since that's more compact
            switch (curve)
            {
                case Ktype.Exponential:
                    return prevKey[1] * Math.Exp(dX * K[0]);
                case Ktype.Logarithmic:
                    return K[0] * Math.Log(X / prevKey[0]) + prevKey[1];
                default:
                    return dX * (K[0] * (X + prevKey[0]) + K[1]) + prevKey[1];
            }
            /* OLD CODE
            if (curve == Ktype.Exponential)
            {
                // Exponential Curve:   Y1/Y0 = EXP(dX * K);
                return prevKey[1] * Math.Exp(dX * K[0]);
            }
            else if (curve == Ktype.Logarithmic)
            {
                // Logarithmic Curve:   dY = K * LN(X1/X0);
                return K[0] * Math.Log(X / prevKey[0]) + prevKey[1];
            }
            else
            {
                // Polynomial Curve:    dY = dX * ( K0 * (X0 + X1) + K1 )
                return dX * (K[0] * (X + prevKey[0]) + K[1]) + prevKey[1];
            }
            */
        }

        //from CashnipLeaf: Is there a better way of doing things than this?
        enum Ktype
        {
            Exponential,
            Logarithmic,
            Polynomial
        }

        // DEBUG

        void PrintCurve(CelestialBody body, string name)
        {
            Debug.Log("AtmosphereTopLayer.PrintCurve", name + " for body " + body.name);
            PrintCurve(body.atmospherePressureCurve, "pressureCurve");
            PrintCurve(body.atmosphereTemperatureCurve, "temperatureCurve");
            PrintCurve(body.atmosphereTemperatureSunMultCurve, "temperatureSunMultCurve");
        }

        void PrintCurve(List<double[]> list, string name) => PrintCurve(WriteCurve(list), name);

        //from CashnipLeaf: this is on its own despite being referenced by only one other override.
        //TODO: inline
        void PrintCurve(ConfigNode config, string name)
        {
            FloatCurve curve = new FloatCurve();
            curve.Load(config);
            PrintCurve(curve, name);
        }

        void PrintCurve(FloatCurve curve, string name)
        {
            ConfigNode config = new ConfigNode();
            curve.Save(config);
            Debug.Log("AtmosphereTopLayer.PrintCurve", name);
            foreach (string key in config.GetValues("key"))
            {
                Debug.Log("AtmosphereTopLayer.PrintCurve", "key = " + key);
            }
        }
    }
}

using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Common_glTF_Exporter.Core;
using Common_glTF_Exporter.Windows.MainWindow;
using Revit_glTF_Exporter;
using Common_glTF_Exporter.Materials;
using Common_glTF_Exporter.Model;
using System.IO.Ports;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using Material = Autodesk.Revit.DB.Material;
using Common_glTF_Exporter.Utils;
using glTF.Manipulator.Schema;
using glTF.Manipulator.GenericSchema;
using glTF.Manipulator.Utils;
using System;


namespace Common_glTF_Exporter.Export
{
    public static class RevitMaterials
    {
        const int ONEINTVALUE = 1;

        public static BaseMaterial ProcessMaterial(MaterialNode node,
                Preferences preferences, Document doc, IndexedDictionary<BaseMaterial> materials,
                List<Texture> textures, List<glTFImage> images)
        {
            BaseMaterial material = new BaseMaterial();
            string materialId = node.MaterialId.ToString();
            material.uuid = materialId;

            var asset = node.GetAppearance();
            var sizeAscc = asset.Size;
            if (materials.Contains(materialId))
            {
                material = materials.GetElement(materialId);
            }
            else
            {
                Autodesk.Revit.DB.Material revitMaterial = doc.GetElement(node.MaterialId) as Autodesk.Revit.DB.Material;

                if (revitMaterial == null)
                {
                    material = ConvertMaterialNodeToBaseMaterial(node, doc);
                    //material = GLTFExportUtils.GetGLTFMaterial(materials);
                }
                else
                {
                    material = RevitMaterials.Export(node, preferences, doc, revitMaterial, textures, images, material);
                }
            }
            materials.AddOrUpdateCurrentMaterial(material.uuid, material, false);

            return material;
        }

        public static BaseMaterial ConvertMaterialNodeToBaseMaterial(
            MaterialNode node,
            Document doc,
            int textureIndex = -1)
        {
            var mat = new BaseMaterial
            {
                uuid = Guid.NewGuid().ToString("N"),
                name = GetMaterialName(node, doc),
                doubleSided = true,
                texCoord = 0
            };

            // -------------------------
            // Base color + alpha
            // -------------------------
            Autodesk.Revit.DB.Color c = node.Color;
            float alpha = (float)(1.0 - node.Transparency);

            mat.baseColorFactor = new List<float>
                    {
                        c.Red   / 255f,
                        c.Green / 255f,
                        c.Blue  / 255f,
                        alpha
                    };

            // -------------------------
            // Roughness (Glossiness)
            // -------------------------
            float glossiness = node.Glossiness / 100f;
            float smoothness = node.Smoothness / 100f;

            float roughness = 1.0f - glossiness;
            roughness *= (1.0f - smoothness * 0.5f);

            mat.roughnessFactor = Clamp(roughness, 0.04f, 1.0f);

            // -------------------------
            // Alpha mode
            // -------------------------
            if (alpha < 1.0f)
            {
                mat.alphaMode = "BLEND";
            }
            else
            {
                mat.alphaMode = "OPAQUE";
            }

            // -------------------------
            // Texture
            // -------------------------
            mat.hasTexture = textureIndex >= 0;
            mat.textureIndex = textureIndex;

            return mat;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static string GetMaterialName(MaterialNode node, Document doc)
        {
            if (node.MaterialId == null || node.MaterialId == ElementId.InvalidElementId)
                return "DefaultMaterial";

            return doc.GetElement(node.MaterialId)?.Name ?? "Material";
        }

        /// <summary>
        /// Export Revit materials.
        /// </summary>
        public static BaseMaterial Export(MaterialNode node,
            Preferences preferences, Document doc,
            Material revitMaterial, List<Texture> textures,
            List<glTFImage> images, BaseMaterial material)
        {

            float opacity = ONEINTVALUE - (float)node.Transparency;

            material.name = revitMaterial.Name;
            MaterialProperties.SetProperties(revitMaterial, opacity, ref material);

            (Autodesk.Revit.DB.Color, Autodesk.Revit.DB.Color) baseNTintColour = (null, null);

            if (revitMaterial != null && preferences.materials == MaterialsEnum.textures)
            {
                baseNTintColour = MaterialTextures.SetMaterialTextures(revitMaterial, material, doc, opacity, textures, images);
                material.baseColorFactor = MaterialProperties.GetDefaultColour(opacity);
            }

            if (material.hasTexture)
            {
                material.baseColorFactor = MaterialProperties.GetDefaultColour(opacity);
            }
            else
            {
                material.baseColorFactor = MaterialProperties.SetMaterialColour(node, opacity, baseNTintColour.Item1, baseNTintColour.Item2);
            }

            MaterialProperties.SetPropertiesFromAsset(doc, revitMaterial, ref material);

            return material;
        }
    }
}


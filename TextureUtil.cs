using ColossalFramework.UI;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ZoneInfo
{
    /// <summary>
    ///  utilities for getting textures (i.e. images)
    /// </summary>
    /// <remarks>logic adapated from TMPE mod</remarks>
    public static class TextureUtil
    {
        public static readonly Texture2D ActivationButtonTexture2D;

        /// <summary>
        /// upon instantiation, load the activation button resource
        /// </summary>
        static TextureUtil()
        {
            // activation button images
            ActivationButtonTexture2D = GetDllResource("ActivationButtonImages.png", 184, 46);
            if (ActivationButtonTexture2D == null)
            {
                Debug.LogError($"Unable to get DLL resource for activation button.");
                return;
            }
            ActivationButtonTexture2D.name = "ActivationButtonImages";
        }

        /// <summary>
        /// generate an atlas from a linearly arranged resource
        /// </summary>
        public static UITextureAtlas GenerateLinearAtlas(string name, Texture2D texture, int numSprites, string[] spriteNames)
        {
            return Generate2DAtlas(name, texture, numSprites, 1, spriteNames);
        }
        
        /// <summary>
        /// generate an atlas from a 2-dimensionally arranged resource
        /// </summary>
        public static UITextureAtlas Generate2DAtlas(string name, Texture2D texture, int numX, int numY, string[] spriteNames)
        {
            // check arguments
            if (spriteNames.Length != numX * numY)
            {
                throw new ArgumentException(
                    "Number of sprite name does not match dimensions " +
                    $"(expected {numX} x {numY}, was {spriteNames.Length})");
            }
            
            // create a new atlas
            UITextureAtlas atlas = ScriptableObject.CreateInstance<UITextureAtlas>();
            if (atlas == null)
            {
                Debug.LogError($"Unable to get DLL resource for activation button.");
                return null;
            }
            atlas.padding = 0;
            atlas.name = name;

            // initialize the material
            Shader shader = Shader.Find("UI/Default UI Shader");
            if (shader != null)
            {
                atlas.material = new Material(shader);
            }
            atlas.material.mainTexture = texture;

            // loop over the X
            int spriteWidth = Mathf.RoundToInt(texture.width / (float)numX);
            int spriteHeight = Mathf.RoundToInt(texture.height / (float)numY);
            int k = 0;
            for (int i = 0; i < numX; ++i)
            {
                // compute X position
                float x = i / (float)numX;

                // loop over the Y
                for (int j = 0; j < numY; ++j)
                {
                    // compute Y position
                    float y = j / (float)numY;

                    // create a new sprite
                    UITextureAtlas.SpriteInfo sprite = new UITextureAtlas.SpriteInfo
                    {
                        name = spriteNames[k],
                        region = new Rect(
                            x,
                            y,
                            spriteWidth / (float)texture.width,
                            spriteHeight / (float)texture.height)
                    };

                    // set the sprite texture by copying the appropriate pixels from the resource texture
                    Texture2D spriteTexture = new Texture2D(spriteWidth, spriteHeight);
                    spriteTexture.SetPixels(
                        texture.GetPixels(
                            (int)(texture.width * sprite.region.x),
                            (int)(texture.height * sprite.region.y),
                            spriteWidth,
                            spriteHeight));
                    sprite.texture = spriteTexture;

                    // add the sprite to the atlas
                    atlas.AddSprite(sprite);
                    ++k;
                }
            }

            // return the new atlas
            return atlas;
        }

        /// <summary>
        /// get an embedded image resource from the DLL
        /// </summary>
        private static Texture2D GetDllResource(string resourceName, int width, int height)
        {
            try
            {
                // prefix namespace to resource name
                resourceName = "ZoneInfo." + resourceName;

                // get the assembly
                Assembly assembly = Assembly.GetExecutingAssembly();
                if (assembly == null)
                {
                    Debug.LogError($"Error getting DLL resource [{resourceName}]: unable to get executing assembly.");
                    return null;
                }

                // get the resource stream
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Debug.LogError($"Error getting DLL resource [{resourceName}]: unable to get resource stream.");
                        return null;
                    }

                    // get the resource from the resource stream
                    using (MemoryStream ms = new MemoryStream())
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, bytesRead);
                        }
                        byte[] resource = ms.ToArray();

                        // load the resource into a new texture
                        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                        if (!texture.LoadImage(resource))
                        {
                            Debug.LogError($"Error getting DLL resource [{resourceName}]: unable to load image into texture.");
                            return null;
                        }

                        // return the texture
                        return texture;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return null;
            }
        }
    }
}

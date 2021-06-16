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
        /// <summary>
        /// get an embedded image resource from the DLL
        /// </summary>
        public static Texture2D GetDllResource(string resourceName, int width, int height)
        {
            try
            {
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

        /// <summary>
        /// generate an atlas from a 1-dimensional resource arranged horizontally
        /// </summary>
        public static UITextureAtlas GenerateAtlasFromHorizontalResource(string atlasName, Texture2D texture, int numSprites, string[] spriteNames)
        {
            return GenerateAtlasFrom2DResource(atlasName, texture, numSprites, 1, spriteNames);
        }
        
        /// <summary>
        /// generate an atlas from a 2-dimensional resource
        /// images are obtained from the texture across (x) then down (y)
        /// </summary>
        public static UITextureAtlas GenerateAtlasFrom2DResource(string atlasName, Texture2D texture, int numX, int numY, string[] spriteNames)
        {
            // check arguments
            if (spriteNames.Length != numX * numY)
            {
                throw new ArgumentException($"Number of sprite names [{spriteNames.Length}] does not match dimensions: x={numX} y={numY}.");
            }
            
            // create a new atlas
            UITextureAtlas atlas = ScriptableObject.CreateInstance<UITextureAtlas>();
            if (atlas == null)
            {
                Debug.LogError($"Unable to create new atlas.");
                return null;
            }
            atlas.name = atlasName;
            atlas.padding = 0;

            // initialize the material
            Shader shader = Shader.Find("UI/Default UI Shader");
            if (shader != null)
            {
                atlas.material = new Material(shader);
            }
            atlas.material.mainTexture = texture;

            // compute sprite width and height
            int spriteWidth  = Mathf.RoundToInt(texture.width  / (float)numX);
            int spriteHeight = Mathf.RoundToInt(texture.height / (float)numY);

            // loop over the X
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
    }
}

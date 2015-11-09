﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2015 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    Nystul
// 
// Notes:
//

using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Player;
using DaggerfallWorkshop.Game.Entity;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    /// <summary>
    /// Implements indoor and dungeon automap window.
    /// </summary>
    public class DaggerfallAutomapWindow : DaggerfallPopupWindow
    {
        const string nativeImgName = "AMAP00I0.IMG";
        const string nativeImgNameGrid3D = "AMAP01I0.IMG";

        enum AutomapViewMode { View2D = 0, View3D = 1};
        AutomapViewMode automapViewMode = AutomapViewMode.View2D;

        Panel panelAutomap = null;

        Texture2D nativeTexture;
        Texture2D nativeTextureGrid2D;
        Texture2D nativeTextureGrid3D;

        Color[] pixelsGrid2D;
        Color[] pixelsGrid3D;

        Camera cameraAutomap = null;
        RenderTexture renderTextureAutomap = null;
        Texture2D textureAutomap = null;


        int renderTextureAutomapWidth;
        int renderTextureAutomapHeight;
        int renderTextureAutomapDepth = 16;

        public DaggerfallAutomapWindow(IUserInterfaceManager uiManager)
            : base(uiManager)
        {
        }

        protected override void Setup()
        {
            ImgFile imgFile = null;
            DFBitmap bitmap = null;

            // Load native texture
            imgFile = new ImgFile(Path.Combine(DaggerfallUnity.Instance.Arena2Path, nativeImgName), FileUsage.UseMemory, false);
            imgFile.LoadPalette(Path.Combine(DaggerfallUnity.Instance.Arena2Path, imgFile.PaletteName));
            bitmap = imgFile.GetDFBitmap();
            nativeTexture = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.ARGB32, false);
            nativeTexture.SetPixels32(imgFile.GetColor32(bitmap, 0));
            nativeTexture.Apply(false, false); // make readable
            nativeTexture.filterMode = DaggerfallUI.Instance.GlobalFilterMode;
            if (!nativeTexture)
                throw new Exception("DaggerfallAutomapWindow: Could not load native texture (AMAP00I0.IMG).");
            
            // Load alternative Grid Icon (3D View Grid graphics)
            imgFile = new ImgFile(Path.Combine(DaggerfallUnity.Instance.Arena2Path, nativeImgNameGrid3D), FileUsage.UseMemory, false);
            imgFile.LoadPalette(Path.Combine(DaggerfallUnity.Instance.Arena2Path, imgFile.PaletteName));
            bitmap = imgFile.GetDFBitmap();
            nativeTextureGrid3D = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.ARGB32, false);
            nativeTextureGrid3D.SetPixels32(imgFile.GetColor32(bitmap, 0));
            nativeTextureGrid3D.Apply(false, false); // make readable
            nativeTextureGrid3D.filterMode = DaggerfallUI.Instance.GlobalFilterMode;
            if (!nativeTextureGrid3D)
                throw new Exception("DaggerfallAutomapWindow: Could not load native texture (AMAP01I0.IMG).");
            pixelsGrid3D = nativeTextureGrid3D.GetPixels(0, 0, 27, 19);

            // Cut out 2D View Grid graphics from background image
            nativeTextureGrid2D = new Texture2D(27, 19, TextureFormat.ARGB32, false);
            nativeTextureGrid2D.filterMode = FilterMode.Point;
            pixelsGrid2D = nativeTexture.GetPixels(78, nativeTexture.height - 171 - 19, 27, 19);
            nativeTextureGrid2D.SetPixels(pixelsGrid2D);
            
            // Always dim background
            ParentPanel.BackgroundColor = ScreenDimColor;

            // Setup native panel background
            NativePanel.BackgroundTexture = nativeTexture;

            // Grid button (toggle 2D <-> 3D view)
            Button gridButton = DaggerfallUI.AddButton(new Rect(78, 171, 27, 19), NativePanel);
            gridButton.OnMouseClick += GridButton_OnMouseClick;

            // Exit button
            Button exitButton = DaggerfallUI.AddButton(new Rect(281, 171, 28, 19), NativePanel);
            exitButton.OnMouseClick += ExitButton_OnMouseClick;

            if (!cameraAutomap)
            {
                GameObject gameObjectCameraAutomap = new GameObject("cameraAutomap");
                cameraAutomap = gameObjectCameraAutomap.AddComponent<Camera>();
                cameraAutomap.clearFlags = CameraClearFlags.SolidColor;
                cameraAutomap.cullingMask = 1;
                //cameraAutomap.depth = 10;
                cameraAutomap.renderingPath = RenderingPath.DeferredLighting;
                cameraAutomap.nearClipPlane = 0.7f;
                cameraAutomap.farClipPlane = 1000.0f;
                //cameraAutomap.orthographic = true;

                //cameraAutomap.transform.position = GameObject.Find("PlayerAdvanced").transform.position;
                //cameraAutomap.transform.rotation = GameObject.Find("PlayerAdvanced").transform.rotation;
            }

            renderTextureAutomapWidth = ParentPanel.InteriorWidth - 80;
            renderTextureAutomapHeight = ParentPanel.InteriorHeight - 100;

            if (!renderTextureAutomap)
                renderTextureAutomap = new RenderTexture(renderTextureAutomapWidth, renderTextureAutomapHeight, renderTextureAutomapDepth);
            cameraAutomap.targetTexture = renderTextureAutomap;

            Rect position = new Rect(40f, 10f, (float)renderTextureAutomapWidth, (float)renderTextureAutomapHeight);
            if (panelAutomap == null)
                panelAutomap = DaggerfallUI.AddPanel(position, ParentPanel);

            if (!textureAutomap)
                textureAutomap = new Texture2D(renderTextureAutomap.width, renderTextureAutomap.height, TextureFormat.ARGB32, false);

            updateAutoMapView();
        }

        
        public override void OnPush()
        {

            if (IsSetup)
            {
                
            }
            updateAutoMapView();
        }

        /*
        public override void Update()
        {
            base.Update();
            //updateAutoMapView();
        }
        */     

        #region Private Methods

        private void updateAutoMapView()
        {
            if ((!cameraAutomap) || (!renderTextureAutomap))
                return;

            cameraAutomap.transform.position = Camera.main.transform.position - Camera.main.transform.forward * 10.0f;
            cameraAutomap.transform.rotation = Camera.main.transform.rotation;

            cameraAutomap.Render();

            RenderTexture.active = renderTextureAutomap;
            textureAutomap.ReadPixels(new Rect(0, 0, renderTextureAutomap.width, renderTextureAutomap.height), 0, 0);
            textureAutomap.Apply(false);

            panelAutomap.BackgroundTexture = textureAutomap;
        }


        #endregion

        #region Event Handlers

        private void GridButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            int numberOfViewModes = Enum.GetNames(typeof(AutomapViewMode)).Length;
            automapViewMode++;
            if ((int)automapViewMode > numberOfViewModes - 1) // first mode is mode 0 -> so use numberOfViewModes-1 for comparison
                automapViewMode = 0;
            switch(automapViewMode)
            {
                case AutomapViewMode.View2D:
                    // update grid graphics
                    nativeTexture.SetPixels(78, nativeTexture.height - 171 - 19, 27, 19, pixelsGrid2D);
                    nativeTexture.Apply(false);
                    break;
                case AutomapViewMode.View3D:
                    // update grid graphics
                    nativeTexture.SetPixels(78, nativeTexture.height - 171 - 19, 27, 19, pixelsGrid3D);
                    nativeTexture.Apply(false);
                    break;
                default:
                    break;
            }
        }

        private void ExitButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            CloseWindow();
        }

        #endregion
    }
}
﻿using System;
using System.Drawing;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace SimpleScene.Demos
{
    public class SLaserEmissionFlareUpdater : ISSpriteUpdater
    {
        protected readonly SLaser _laser;
        protected readonly int _beamId;
        protected readonly SSObjectOcclusionQueuery _occDiskObj;

        protected readonly RectangleF _backgroundRect;
        protected readonly RectangleF _overlayRect;
        protected int _backgroundSpriteIdx;
        protected int _overlaySpriteIdx;


        public SLaserEmissionFlareUpdater(
            SLaser laser, int beamId, 
            /*SSObjectOcclusionQueuery occFlat,*/ SSObjectOcclusionQueuery occDisk, 
            RectangleF backgroundRect,
            RectangleF overlayRect
        )
        {
            this._laser = laser;
            this._beamId = beamId;
            //this._beamOccFlatObj = occFlat;
            this._occDiskObj = occDisk;

            this._backgroundRect = backgroundRect;
            this._overlayRect = overlayRect;
        }

        public SLaserEmissionFlareUpdater(
            SLaser laser, int beamId, 
            /*SSObjectOcclusionQueuery occFlat,*/ SSObjectOcclusionQueuery occPersp
        ) 
            : this(laser, beamId, /*occFlat,*/ occPersp,
                new RectangleF(0f, 0.5f, 0.5f, 0.5f), new RectangleF(0f, 0.5f, 0.5f, 0.5f))
        { }

        public void setupSprites(SInstancedSpriteData instanceData)
        {
            _backgroundSpriteIdx = instanceData.requestSlot();
            _overlaySpriteIdx = instanceData.requestSlot();

            instanceData.writeRect(_backgroundSpriteIdx, _backgroundRect);
            instanceData.writeRect(_overlaySpriteIdx, _overlayRect);

        }

        public void releaseSprites(SInstancedSpriteData instanceData)
        {
            instanceData.releaseSlot(_backgroundSpriteIdx);
            instanceData.releaseSlot(_overlaySpriteIdx);
        }

        public void updateSprites(SInstancedSpriteData instanceData, ref RectangleF screenClientRect,
                                  ref Matrix4 camera3dView, ref Matrix4 camera3dProj)
        {
            var laserParams = _laser.parameters;
            var beam = _laser.beam(_beamId);

            float screenAreaUsed = (float)_occDiskObj.OcclusionQueueryResult;
            if (screenAreaUsed <= 0f)
                return;

            // position sprites at the beam start in screen space
            Matrix4 camera3dViewProjMat = camera3dView * camera3dProj;
            var beamStartScreen = OpenTKHelper.WorldToScreen(beam.startPos, 
                                      ref camera3dViewProjMat, ref screenClientRect);
            instanceData.writePosition(_backgroundSpriteIdx, beamStartScreen);
            instanceData.writePosition(_overlaySpriteIdx, beamStartScreen);

            // compute screen space needed to occupy the area where the start of the middle's crossbeam
            // would be displayed
            Matrix4 viewInverted = camera3dView.Inverted();
            Vector3 viewRight = Vector3.Transform(Vector3.UnitX, viewInverted).Normalized();
            Vector3 occRightMost = _occDiskObj.Pos + viewRight * laserParams.beamBackgroundWidth;
            Vector2 occRightMostPt = OpenTKHelper.WorldToScreen(occRightMost, 
                                         ref camera3dViewProjMat, ref screenClientRect);
            Vector2 occCenterPt = OpenTKHelper.WorldToScreen(_occDiskObj.Pos, 
                                      ref camera3dViewProjMat, ref screenClientRect);
            float screenFullRadius = Math.Abs(occRightMostPt.X - occCenterPt.X);

            // write sprite size big enough to cover up the starting section of the cross beam (middle)
            float scale = Math.Max(laserParams.emissionFlareScreenSizeMin, screenFullRadius * 1.5f);
            instanceData.writeMasterScale(_backgroundSpriteIdx, scale * 1.2f);
            instanceData.writeMasterScale(_overlaySpriteIdx, scale * 1f);

            // add some variety to orientation to make the sprites look less static
            float beamIntensity = _laser.envelopeIntensity * beam.periodicIntensity;
            instanceData.writeOrientationZ(_backgroundSpriteIdx, beamIntensity * 8f * (float)Math.PI);
            instanceData.writeOrientationZ(_overlaySpriteIdx, beamIntensity * 8f * (float)Math.PI);

            // color intensity: depends on the dot product between to-camera vector and beam direction;
            // also depends on how of the occlusion disk area is visible
            float maxScreenArea = (float)Math.PI 
                * laserParams.occDiskRadiusPx * laserParams.occDiskRadiusPx;
            float occDiskAreaRatio = screenAreaUsed / maxScreenArea;

            Vector3 toCamera = (Vector3.Transform(Vector3.Zero, camera3dView.Inverted()) - beam.startPos)
                .Normalized();
            float dot = Math.Max(0f, Vector3.Dot(toCamera, beam.direction()));

            // finish background color
            float occColorRatioBackground = occDiskAreaRatio * (float)Math.Pow(dot, 1.0);
            var colorIntensityBackground = beamIntensity * occColorRatioBackground;
            var backgroundColor = laserParams.backgroundColor;
            backgroundColor.A = Math.Min(colorIntensityBackground, 1f);
            instanceData.writeColor(_backgroundSpriteIdx, backgroundColor);

            // finish overlay color
            float occColorRatioOverlay = occDiskAreaRatio * (float)Math.Pow(dot, 0.5);
            var colorIntensityOverlay = (float)Math.Pow(beamIntensity, 0.4) * occColorRatioOverlay;
            var overlayColor = laserParams.overlayColor;
            overlayColor.A = Math.Min(colorIntensityOverlay, 1f);
            instanceData.writeColor(_overlaySpriteIdx, overlayColor);
        }
    }
}


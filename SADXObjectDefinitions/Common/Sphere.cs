﻿using System;
using System.Collections.Generic;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using SonicRetro.SAModel.Direct3D;
using SonicRetro.SAModel.SADXLVL2;
using SonicRetro.SAModel.SAEditorCommon.SETEditing;
using SonicRetro.SAModel.SAEditorCommon.DataTypes;

namespace SADXObjectDefinitions.Common
{
    public class Sphere : ObjectDefinition
    {
        private SonicRetro.SAModel.Object model;
        private Microsoft.DirectX.Direct3D.Mesh[] meshes;

        public override void Init(ObjectData data, string name, Device dev)
        {
            model = ObjectHelper.LoadModel("Objects/Collision/Sphere.sa1mdl");
            meshes = ObjectHelper.GetMeshes(model, dev);
        }

        public override HitResult CheckHit(SETItem item, Vector3 Near, Vector3 Far, Viewport Viewport, Matrix Projection, Matrix View, MatrixStack transform)
        {
            transform.Push();
            transform.NJTranslate(item.Position);
            transform.NJRotateY(item.Rotation.Y);
            transform.NJScale((item.Scale.X + 10) / 5f, (item.Scale.X + 10) / 5f, (item.Scale.X + 10) / 5f);
            HitResult result = model.CheckHit(Near, Far, Viewport, Projection, View, transform, meshes);
            transform.Pop();
            return result;
        }

		public override RenderInfo[] Render(SETItem item, Device dev, EditorCamera camera, MatrixStack transform, bool selected)
        {
            List<RenderInfo> result = new List<RenderInfo>();
            transform.Push();
            transform.NJTranslate(item.Position);
            transform.NJRotateY(item.Rotation.Y);
            transform.NJScale((item.Scale.X + 10) / 5f, (item.Scale.X + 10) / 5f, (item.Scale.X + 10) / 5f);
            result.AddRange(model.DrawModelTree(dev, transform, null, meshes));
            if (selected)
                result.AddRange(model.DrawModelTreeInvert(dev, transform, meshes));
            transform.Pop();
            return result.ToArray();
        }

		public override SonicRetro.SAModel.BoundingSphere GetBounds(SETItem item)
		{
			float largestScale = (item.Scale.X + 10) / 5f;
			if (item.Scale.Y > largestScale) largestScale = (item.Scale.Y + 10) / 5f;
			if (item.Scale.Z > largestScale) largestScale = (item.Scale.Z + 10) / 5f;

			SonicRetro.SAModel.BoundingSphere sphere = new SonicRetro.SAModel.BoundingSphere() { Center = new SonicRetro.SAModel.Vertex(item.Position.X, item.Position.Y, item.Position.Z), Radius = (largestScale / 2) };

			return sphere;
		}

        public override string Name { get { return "Solid Sphere"; } }
    }
}
﻿using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhongSuHuynhProject1
{

    class RecJig : DrawJig
    {
        public Point3d firstPoint;
        public Point3d secondPoint;
        public List<Point3d> primaryBoundary = new List<Point3d>();
        //THIS IS HOW YOU MAKE IT DONE

        public RecJig(Point3d firstPoint)
        {
            this.firstPoint = firstPoint;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {

            JigPromptPointOptions jigOpts = new JigPromptPointOptions();
            jigOpts.UserInputControls = (
                UserInputControls.Accept3dCoordinates |
                UserInputControls.UseBasePointElevation
                );

            jigOpts.Message = "\nSpecify second point: ";

            PromptPointResult promptPointResult = prompts.AcquirePoint(jigOpts);
            if(promptPointResult.Status == PromptStatus.OK)
            {
                secondPoint = promptPointResult.Value;
                return SamplerStatus.OK;
            }
            else
            {
                return SamplerStatus.Cancel;
            }
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            WorldGeometry geo = draw.Geometry;
            List<Point3d> points = new List<Point3d>();
            points.Add(firstPoint);
            points.Add(secondPoint);
            primaryBoundary = createRectangleForXclip(points);

            if (primaryBoundary == null || primaryBoundary.Count != 4) return false;

            geo.WorldLine(primaryBoundary[0], primaryBoundary[1]);
            geo.WorldLine(primaryBoundary[1], primaryBoundary[2]);
            geo.WorldLine(primaryBoundary[2], primaryBoundary[3]);
            geo.WorldLine(primaryBoundary[3], primaryBoundary[0]);

            return true;
        }

        public List<Point3d> createRectangleForXclip(List<Point3d> boundary)
        {
            if (boundary.Count != 2) return null;

            Point3d point2 = new Point3d(boundary[1].X, boundary[0].Y, 0);
            Point3d point4 = new Point3d(boundary[0].X, boundary[1].Y, 0);

            //Point2d point2_1 = new Point2d(boundary[0].X, boundary[1].Y);
            //Point2d point4_1 = new Point2d(boundary[1].X, boundary[0].Y);

            if ((boundary[0].X < boundary[1].X && boundary[0].Y < boundary[1].Y) ||
                (boundary[0].X > boundary[1].X && boundary[0].Y > boundary[1].Y))
            {
                boundary.Insert(1, point2);
                boundary.Insert(3, point4);
            }
            else if (
              (boundary[0].X < boundary[1].X && boundary[0].Y > boundary[1].Y) ||
              (boundary[0].X > boundary[1].X && boundary[0].Y < boundary[1].Y)
              )
            {
                boundary.Insert(1, point4);
                boundary.Insert(3, point2);
            }
            return boundary;
        }
    }

    class DragEntities : DrawJig
    {
        List<BlockReference> brefs = new List<BlockReference>();
        List<Line> lines = new List<Line>();

        List<Point3d> blockPosition = new List<Point3d>();
        List<Tuple<Point3d, Point3d>> linesPosition = new List<Tuple<Point3d, Point3d>>();
        List<Circle> circles = new List<Circle>();
        Point3d basePoint;
        Point3d secondPoint;

        public DragEntities(List<BlockReference> brefs, List<Line> lines, Point3d basePoint)
        {
            this.basePoint = basePoint;
            this.brefs = brefs;
            this.lines = lines;

            foreach(BlockReference bref in brefs)
            {
                blockPosition.Add(new Point3d(bref.Position.X, bref.Position.Y, 0));
            }
            foreach(Line line in lines)
            {
                linesPosition.Add(new Tuple<Point3d, Point3d>(line.StartPoint, line.EndPoint));
            }
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            JigPromptPointOptions pointOp = new JigPromptPointOptions("\nSpecify second point: ");
            pointOp.UserInputControls = (
                UserInputControls.Accept3dCoordinates |
                UserInputControls.UseBasePointElevation
                );

            pointOp.BasePoint = basePoint;
            pointOp.UseBasePoint = true;

            PromptPointResult result = prompts.AcquirePoint(pointOp);
            if (result.Status == PromptStatus.OK)
            {
                secondPoint = result.Value;
                return SamplerStatus.OK;
            }
            else return SamplerStatus.Cancel;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            WorldGeometry geo = draw.Geometry;
            if(geo != null)
            {
                Vector3d vec = basePoint.GetVectorTo(secondPoint);
                Matrix3d disp = Matrix3d.Displacement(this.basePoint.GetVectorTo(this.secondPoint));
                //geo.PushModelTransform(disp);
                for (int i = 0; i < blockPosition.Count; i++)
                {
                    Point3d tempPoint = blockPosition[i] + vec;
                    brefs[i].Position = tempPoint;
                    geo.Draw(brefs[i]);
                }

                for(int i = 0; i < linesPosition.Count; i++)
                {
                    Point3d tempStartPoint = linesPosition[i].Item1 + vec;
                    Point3d tempEndPoint = linesPosition[i].Item2 + vec;

                    lines[i].StartPoint = tempStartPoint;
                    lines[i].EndPoint = tempEndPoint;
                    geo.Draw(lines[i]);
                }
                //geo.PopModelTransform();
                return true;
            }
            return false;
        }
    }
}

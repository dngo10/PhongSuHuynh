﻿using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhongSuHuynhProject1
{

    //block GE_POST and GE_DBLSTUD
    public partial class Main
    {
        const string filterDictName = "ACAD_FILTER";
        const string spatialName = "SPATIAL";
        const string swallLayer = "FRAME_SWALL";

        const string gePost = "GE_POST";
        const string geDBLSTUD = "GE_DBLSTUD";
        const string geTRISTUD = "GE_TRISTUD";

        const string frameText = "FRAME_TEXT";

        const string linkDwg = "K:\\DANIELSOFT\\PhongBlock_1.dwg";

        private List<Point3d> m_pts = new List<Point3d>();

        void ed_PointMonitor(object sender, PointMonitorEventArgs e)
        {
            Point3d ptLast = e.Context.LastPoint;
            if(ptLast != Point3d.Origin && m_pts.Contains(e.Context.LastPoint) == false)
            {
                m_pts.Add(ptLast);
            }
        }

        bool getSelectedWindow(ref List<Point3d> boundary, ref Editor ed, ref Database db)
        {
            
            PromptSelectionOptions pso = new PromptSelectionOptions();
            pso.MessageForAdding = "Select Objects: ";
            pso.SingleOnly = false;
            PromptSelectionResult psr = ed.GetSelection(pso);
            if (psr.Status != PromptStatus.OK) return false;
            SelectionSetDelayMarshalled ssMarshal = (SelectionSetDelayMarshalled)psr.Value;
            AdsName name = ssMarshal.Name;
            SelectionSet selSet = (SelectionSet)ssMarshal;
            foreach (SelectedObject item in selSet)
            {
                boundary.Clear();
                switch (item.SelectionMethod)
                {
                    case SelectionMethod.Crossing:
                        CrossingOrWindowSelectedObject crossSelObj = item as CrossingOrWindowSelectedObject;
                        PickPointDescriptor[] crossSelPickedPoints = crossSelObj.GetPickPoints();
                        foreach (PickPointDescriptor point in crossSelPickedPoints) boundary.Add(point.PointOnLine);
                        break;
                    case SelectionMethod.Window:
                        CrossingOrWindowSelectedObject windSelObj = item as CrossingOrWindowSelectedObject;
                        PickPointDescriptor[] winSelPickedPoints = windSelObj.GetPickPoints();
                        foreach (PickPointDescriptor point in winSelPickedPoints) boundary.Add(point.PointOnLine);
                        break;
                }
            }
            return true;
        }


        [CommandMethod("PhongSuHuynh")]
        public void BoxJig()
        {
            m_pts = new List<Point3d>();
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            //var ppr = ed.GetPoint("\nSpecify first point: ");

            //if (ppr.Status == PromptStatus.OK)
            if (true)
                {

                if(!getSelectedWindow(ref m_pts, ref ed, ref db))
                {
                    return;
                }

                //RecJig rec = new RecJig(ppr.Value);
                //PromptResult promptResult  = ed.Drag(rec);
                //psr.Status == PromptStatus.OK)
                
                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        List<BlockReference> blocks = new List<BlockReference>();
                        List<Line> lines = new List<Line>();

                        HashSet<String> NothingBlocks = new HashSet<string>();

                        //RunCode(rec.firstPoint, rec.secondPoint, tr, ref db, ed, ref blocks, ref lines);
                        RunCode(m_pts, tr, ref db, ed, ref blocks, ref lines, ref NothingBlocks);
                        if (blocks.Count != 0 || lines.Count != 0)
                        {

                            var ppr2 = ed.GetPoint("\nSpecify base point: ");
                            if (ppr2.Status == PromptStatus.OK)
                            {
                                DragEntities dragEntity = new DragEntities(blocks, lines, ppr2.Value);
                                PromptResult result = ed.Drag(dragEntity);
                                if (result.Status != PromptStatus.OK)
                                {
                                    foreach (BlockReference block in blocks)
                                    {
                                        block.Erase();
                                    }

                                    foreach (Line line in lines)
                                    {
                                        line.Erase();
                                    }
                                }
                            }
                            else
                            {
                                foreach (BlockReference block in blocks)
                                {
                                    block.Erase();
                                }

                                foreach (Line line in lines)
                                {
                                    line.Erase();
                                }
                            }
                        }
                        tr.Commit();
                        
                    }
                }
            }
        }

        public void RunCode(List<Point3d> pts, Transaction tr, ref Database db, Editor ed, ref List<BlockReference> InBoundaryAddBrefs, ref List<Line> InBoundaryLines, ref HashSet<String> NothingBlocks)
        {
            //Setup layer and blockTableRecord
            createLayer();
            copyLocktableRecord();
            List<BlockReference> objects = new List<BlockReference>();

            List<Line> sWallLines = new List<Line>();

            TypedValue[] filterList = new TypedValue[1];

            filterList[0] = new TypedValue(0, "INSERT,LINE");
            SelectionFilter filter = new SelectionFilter(filterList);

            HashSet<ObjectId> choosenIds = new HashSet<ObjectId>();

            PromptSelectionResult selRes = ed.SelectCrossingWindow(pts[0], pts[2], filter);

            if (selRes.Status == PromptStatus.OK)
            {
                foreach (ObjectId id in selRes.Value.GetObjectIds())
                {
                    choosenIds.Add(id);
                }
            }

            PromptSelectionResult selResReversed = ed.SelectCrossingWindow(pts[2], pts[0], filter);

            if (selResReversed.Status == PromptStatus.OK)
            {
                foreach (ObjectId id in selResReversed.Value.GetObjectIds())
                {
                    choosenIds.Add(id);
                }
            }

            if (choosenIds.Count == 0) return;


            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord spaceRecord = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
            BlockTableRecord gePostBt = (BlockTableRecord)tr.GetObject(bt[gePost], OpenMode.ForRead);
            BlockTableRecord geDBLSTUDPost = (BlockTableRecord)tr.GetObject(bt[geDBLSTUD], OpenMode.ForRead);
            BlockTableRecord geTRISTUDPost = (BlockTableRecord)tr.GetObject(bt[geTRISTUD], OpenMode.ForRead);

            foreach (ObjectId objectId in choosenIds)
            {
                 DBObject dbObject = tr.GetObject(objectId, OpenMode.ForRead);


                 if(objectId.ObjectClass == RXObject.GetClass(typeof(Line)))
                 {
                     Line line = dbObject as Line;
                     if(line.Layer.Split('|').Last() == swallLayer)
                     {
                         sWallLines.Add(copyLine(spaceRecord, ref line, tr));
                     }
                 }
                 else if(dbObject is BlockReference)
                 {
                    BlockReference bref =dbObject as BlockReference;
                    BlockTableRecord brefRecord = (BlockTableRecord)tr.GetObject(bref.BlockTableRecord, OpenMode.ForRead);

                    string brefName = bref.IsDynamicBlock ? ((BlockTableRecord)bref.DynamicBlockTableRecord.GetObject(OpenMode.ForRead)).Name : bref.Name;
                    if (brefName == gePost)
                    {
                        objects.Add(copyDynamicBlock(spaceRecord, ref bref, ref gePostBt, tr));
                    }
                    else if (brefName == geDBLSTUD)
                    {
                        objects.Add(copyDynamicBlock(spaceRecord, ref bref, ref geDBLSTUDPost, tr));
                    }
                    else if (brefName == geTRISTUD)
                    {
                        objects.Add(copyDynamicBlock(spaceRecord, ref bref, ref geTRISTUDPost, tr));
                    }
                    else
                    {
                        List<BlockReference> rawNewBlocks = new List<BlockReference>();
                        List<Line> newLines = new List<Line>();
                        InspectBlockReference(ref bt, ref tr, ref spaceRecord, ref bref, ref rawNewBlocks, ref newLines , ref NothingBlocks);
                        objects.AddRange(rawNewBlocks);
                        sWallLines.AddRange(newLines);
                    }
                 }
            }


            List<Point3d> boundary = createRectangleForXclip(pts);
            foreach(BlockReference bref in objects)
            {
                if(canAdd(false, boundary, bref.Position))
                {
                    InBoundaryAddBrefs.Add(bref);
               }
               else
               {
                   bref.Erase();
               }
            }

            foreach(Line line in sWallLines)
            {
                if(canAdd(false, boundary, line.StartPoint) || canAdd(false, boundary, line.EndPoint)) {
                    InBoundaryLines.Add(line);
                }
                else
                {
                    line.Erase();
                }
            }

            return;
        }

        //public List<Line> InsectLine(ref Transaction tr, ref BlockTableRecord spaceRecord, ref Line line)
        //{
        //
        //}

        //Carry all.
        public void InspectBlockReference(ref BlockTable bt, ref Transaction tr, ref BlockTableRecord spaceRecord, 
            ref BlockReference bref, ref List<BlockReference> blocks, ref List<Line> lines, ref HashSet<String> NothingBlocks)
        {
            BlockTableRecord gePostBt = (BlockTableRecord)tr.GetObject(bt[gePost], OpenMode.ForRead);
            BlockTableRecord geDBLSTUBPost = (BlockTableRecord)tr.GetObject(bt[geDBLSTUD], OpenMode.ForRead);
            BlockTableRecord geTRISTUDPost = (BlockTableRecord)tr.GetObject(bt[geTRISTUD], OpenMode.ForRead);
            List<Point2d> boundary = new List<Point2d>();
            bool isInverted = false;
            bool hasBoundary = false;

            if (bref != null)
            {
                if (!bref.ExtensionDictionary.IsNull)
                {
                    DBDictionary extDict = (DBDictionary)tr.GetObject(bref.ExtensionDictionary, OpenMode.ForRead);
                    if (extDict != null && extDict.Contains(filterDictName))
                    {
                        DBDictionary fildict = (DBDictionary)tr.GetObject(extDict.GetAt(filterDictName), OpenMode.ForRead);
                        if (fildict != null)
                        {
                            if (fildict.Contains(spatialName))
                            {
                                var fil = (SpatialFilter)tr.GetObject(fildict.GetAt(spatialName), OpenMode.ForRead);
                                if (fil != null)
                                {
                                    Extents3d ext = fil.GetQueryBounds();
                                    isInverted = fil.Inverted;
                                    var pts = fil.Definition.GetPoints();

                                    //Matrix3d inverseMatrix = brefMatrix.Inverse();
                                    foreach (var pt in pts)
                                    {
                                        Point3d point3 = new Point3d(pt.X, pt.Y, 0);
                                        point3 = point3.TransformBy(fil.OriginalInverseBlockTransform);
                                        boundary.Add(new Point2d(point3.X, point3.Y));
                                        //ed.WriteMessage("\nBoundary point at {0}", pt);
                                    }
                                }
                            }
                            if(boundary.Count >= 2)
                            {
                                hasBoundary = true;
                            }
                        }
                    }
                }

                BlockTableRecord newBtr = (BlockTableRecord)tr.GetObject(bref.BlockTableRecord, OpenMode.ForRead);

                foreach (ObjectId id in newBtr)
                {
                    if (id.ObjectClass.DxfName == "INSERT")
                    {
                        BlockReference newBref1 = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
                        Matrix3d matrix3d = newBref1.BlockTransform;
                        string brefName = newBref1.IsDynamicBlock ? ((BlockTableRecord)newBref1.DynamicBlockTableRecord.GetObject(OpenMode.ForRead)).Name : newBref1.Name;
                        if (brefName.Split('|').Last() == gePost && !NothingBlocks.Contains(brefName))
                        {
                            BlockReference bref2 = copyDynamicBlock(spaceRecord, ref newBref1, ref gePostBt, tr);
                            if(!hasBoundary || canAdd(isInverted, boundary, bref2.Position))
                            {
                                blocks.Add(bref2);
                            }
                            else
                            {
                                bref2.Erase();
                            }
                        }
                        else if (brefName.Split('|').Last() == geDBLSTUD && !NothingBlocks.Contains(brefName))
                        {
                            BlockReference bref2 = copyDynamicBlock(spaceRecord, ref newBref1, ref geDBLSTUBPost, tr);
                            if (!hasBoundary || canAdd(isInverted, boundary, bref2.Position))
                            {
                                blocks.Add(bref2);
                            }
                            else
                            {
                                bref2.Erase();
                            }
                        } else if(brefName.Split('|').Last() == geTRISTUD && !NothingBlocks.Contains(brefName))
                        {
                            BlockReference bref2 = copyDynamicBlock(spaceRecord, ref newBref1, ref geTRISTUDPost, tr);
                            if (!hasBoundary || canAdd(isInverted, boundary, bref2.Position))
                            {
                                blocks.Add(bref2);
                            }
                            else
                            {
                                bref2.Erase();
                            }
                        }
                        else
                        {
                            List<BlockReference> newObjects = new List<BlockReference>();
                            List<Line> newLines = new List<Line>();

                            InspectBlockReference(ref bt, ref tr, ref spaceRecord, ref newBref1, ref newObjects, ref newLines, ref NothingBlocks);

                            if(newObjects.Count == 0 && newLines.Count == 0)
                            {
                                NothingBlocks.Add(brefName);
                            }
                            if (hasBoundary)
                            {
                                foreach(BlockReference nbref in newObjects)
                                {
                                    if(canAdd(isInverted, boundary, nbref.Position))
                                    {
                                        blocks.Add(nbref);
                                    }
                                    else
                                    {
                                        nbref.Erase();
                                    }
                                }
                            }
                            else
                            {
                                blocks.AddRange(newObjects);
                            }
                        }
                    }
                    else if (id.ObjectClass == RXObject.GetClass(typeof(Line)))
                    {
                        Line line = (Line)tr.GetObject(id, OpenMode.ForRead);
                        if (line.Layer.Split('|').Last() == swallLayer)
                        {
                            Line newLine = copyLine(spaceRecord, ref line, tr);
                            if (canAdd(isInverted, boundary, newLine.StartPoint) || canAdd(isInverted, boundary, newLine.EndPoint))
                            {
                                lines.Add(newLine);
                            }
                        }
                    }
                }

                for (int i = 0; i < blocks.Count; i++)
                {
                    blocks[i].TransformBy(bref.BlockTransform);
                }

                for (int i = 0; i < lines.Count; i++)
                {
                    lines[i].TransformBy(bref.BlockTransform);
                }
            }
        }

        public bool canAdd(bool isInverted, List<Point2d> boundary, Point3d position)
        {
            if (boundary.Count == 0 || boundary.Count == 1) return false;
            if (boundary.Count == 2)
            {
                boundary = createRectangleForXclip(boundary);
            }

            if (isInverted)
            {
                return !IsPointInPolygon(position, boundary);
            }
            else
            {
                return IsPointInPolygon(position, boundary);
            }

        }

        public bool canAdd(bool isInverted, List<Point3d> boundary, Point3d position)
        {
            if (boundary.Count == 0 || boundary.Count == 1) return false;
            if (boundary.Count == 2)
            {
                boundary = createRectangleForXclip(boundary);
            }

            if (isInverted)
            {
                return !IsPointInPolygon(position, boundary);
            }
            else
            {
                return IsPointInPolygon(position, boundary);
            }

        }

        public List<Point3d> createRectangleForXclip(List<Point3d> boundary)
        {
            if (boundary.Count != 2) return boundary;

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

        public List<Point2d> createRectangleForXclip(List<Point2d> boundary)
        {
            if (boundary.Count != 2) return boundary;

            Point2d point2 = new Point2d(boundary[1].X, boundary[0].Y);
            Point2d point4 = new Point2d(boundary[0].X, boundary[1].Y);

            //Point2d point2_1 = new Point2d(boundary[0].X, boundary[1].Y);
            //Point2d point4_1 = new Point2d(boundary[1].X, boundary[0].Y);

            if ((boundary[0].X < boundary[1].X && boundary[0].Y < boundary[1].Y) || 
                (boundary[0].X > boundary[1].X && boundary[0].Y > boundary[1].Y))
            {
                boundary.Insert(1, point2);
                boundary.Insert(3, point4);   
            } else if(
                (boundary[0].X < boundary[1].X && boundary[0].Y > boundary[1].Y) ||
                (boundary[0].X > boundary[1].X && boundary[0].Y < boundary[1].Y)
                )
            {
                boundary.Insert(1, point4);
                boundary.Insert(3, point2);
            }
            return boundary;
        }

        public BlockReference copyBlockReference(ref BlockReference bref, ref BlockTableRecord br)
        {
            BlockReference newbref = new BlockReference(Point3d.Origin, br.ObjectId);
            newbref.TransformBy(bref.BlockTransform);
            newbref.BlockUnit = bref.BlockUnit;
            newbref.Normal = bref.Normal;
            newbref.Layer = bref.Layer.Split('|').Last();

            return newbref;
        }


        public Line copyLine(BlockTableRecord spaceRecord, ref Line line, Transaction tr)
        {
            Line newLine = new Line();
            newLine.StartPoint = line.StartPoint;
            newLine.EndPoint = line.EndPoint;
            newLine.Normal = line.Normal;
            newLine.Layer = swallLayer;
            spaceRecord.UpgradeOpen();
            spaceRecord.AppendEntity(newLine);
            tr.AddNewlyCreatedDBObject(newLine, true);
            spaceRecord.DowngradeOpen();

            return newLine;
        }

        public BlockReference copyDynamicBlock(BlockTableRecord spaceRecord, ref BlockReference bref, ref BlockTableRecord br, Transaction tr)
        {
            BlockReference newbref = copyBlockReference(ref bref, ref br);
            //newbref.UpgradeOpen();
            //spaceRecord.DowngradeOpen();
            spaceRecord.UpgradeOpen();
            spaceRecord.AppendEntity(newbref);
            tr.AddNewlyCreatedDBObject(newbref, true);
            spaceRecord.DowngradeOpen();

            //newbref.Visible = false;

            for (int i = 0; i < bref.DynamicBlockReferencePropertyCollection.Count; i++)
            {
                for (int j = 0; j < newbref.DynamicBlockReferencePropertyCollection.Count; j++)
                {
                    if (!newbref.DynamicBlockReferencePropertyCollection[j].ReadOnly &&
                        !bref.DynamicBlockReferencePropertyCollection[i].ReadOnly &&
                        newbref.DynamicBlockReferencePropertyCollection[j].PropertyName == bref.DynamicBlockReferencePropertyCollection[i].PropertyName
                        )
                    {
                        newbref.DynamicBlockReferencePropertyCollection[j].Value = bref.DynamicBlockReferencePropertyCollection[i].Value;
                    }
                }
            }
            return newbref;
        }

        public void copyLocktableRecord()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            using(Database OpenDb = new Database(false, true))
            {
                OpenDb.ReadDwgFile(linkDwg, System.IO.FileShare.Read, true, "");

                ObjectIdCollection ids = new ObjectIdCollection();
                using(Transaction tr = OpenDb.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(OpenDb.BlockTableId, OpenMode.ForRead);

                    if (bt.Has(geDBLSTUD))
                    {
                        ids.Add(bt[geDBLSTUD]);
                    }

                    if (bt.Has(gePost))
                    {
                        ids.Add(bt[gePost]);
                    }

                    if (bt.Has(geTRISTUD))
                    {
                        ids.Add(bt[geTRISTUD]);
                    }

                    tr.Commit();
                }
                if(ids.Count != 0)
                {
                    Database destdb = doc.Database;
                    IdMapping iMap = new IdMapping();
                    destdb.WblockCloneObjects(ids, destdb.BlockTableId, iMap, DuplicateRecordCloning.Ignore, false);
                }
            }
        }

        public void createLayer()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;

            using(Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(frameText))
                {
                    LayerTableRecord ltr = new LayerTableRecord();
                    ltr.Name = frameText;
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                    lt.UpgradeOpen();
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                    tr.Commit();
                }
            }
        }

        public bool IsPointInPolygon(Point3d p, List<Point2d> polygon)
        {
            double minX = polygon[0].X;
            double maxX = polygon[0].X;
            double minY = polygon[0].Y;
            double maxY = polygon[0].Y;
            for (int i = 1; i < polygon.Count; i++)
            {
                Point2d q = polygon[i];
                minX = Math.Min(q.X, minX);
                maxX = Math.Max(q.X, maxX);
                minY = Math.Min(q.Y, minY);
                maxY = Math.Max(q.Y, maxY);
            }

            if (p.X < minX || p.X > maxX || p.Y < minY || p.Y > maxY)
            {
                return false;
            }

            // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if ((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y) &&
                     p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public bool IsPointInPolygon(Point3d p, List<Point3d> polygon)
        {
            double minX = polygon[0].X;
            double maxX = polygon[0].X;
            double minY = polygon[0].Y;
            double maxY = polygon[0].Y;
            for (int i = 1; i < polygon.Count; i++)
            {
                Point3d q = polygon[i];
                minX = Math.Min(q.X, minX);
                maxX = Math.Max(q.X, maxX);
                minY = Math.Min(q.Y, minY);
                maxY = Math.Max(q.Y, maxY);
            }

            if (p.X < minX || p.X > maxX || p.Y < minY || p.Y > maxY)
            {
                return false;
            }

            // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if ((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y) &&
                     p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}

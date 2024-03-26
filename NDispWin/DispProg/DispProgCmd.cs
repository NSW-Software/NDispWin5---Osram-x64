﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Reflection;

namespace NDispWin
{
    using NSW.Net;

    class DispProgCmd
    {
    }

    internal class GroupDisp
    {
        public static bool Execute(DispProg.TLine Line, ERunMode RunMode, double f_origin_x, double f_origin_y, double f_origin_z)
        {
            if (GDefine.GantryConfig == GDefine.EGantryConfig.XY_ZX2Y2_Z2) throw new Exception("Group Disp do not support Dual Head Config.");

            string EMsg = Line.Cmd.ToString();

            try
            {
                GDefine.Status = EStatus.Busy;

                bool b_Head2IsValid = false;
                bool b_SyncHead2 = false;
                bool[] b_HeadRun = new bool[2] { false, false };
                if (!DispProg.SelectHead(Line, ref b_HeadRun, ref b_Head2IsValid, ref b_SyncHead2)) goto _End;

                TModelPara Model = new TModelPara(DispProg.ModelList, Line.IPara[0]);
                bool Disp = (Line.IPara[2] > 0);

                #region Move GZ2 Up if invalid
                if (GDefine.GantryConfig == GDefine.EGantryConfig.XY_ZX2Y2_Z2 && !b_Head2IsValid)
                {
                    switch (RunMode)
                    {
                        case ERunMode.Normal:
                        case ERunMode.Dry:
                            if (!TaskDisp.TaskMoveGZ2Up()) return false;
                            break;
                    }
                }
                #endregion

                #region assign and translate position
                double dx = f_origin_x + DispProg.rt_LayoutRelPos[DispProg.RunTime.UIndex].X + Line.X[0];
                double dy = f_origin_y + DispProg.rt_LayoutRelPos[DispProg.RunTime.UIndex].Y + Line.Y[0];
                DispProg.TranslatePos(dx, dy, DispProg.rt_Head1RefData, ref dx, ref dy);

                double dx2 = f_origin_x + DispProg.rt_LayoutRelPos[DispProg.RunTime.UIndex2].X + Line.X[0];
                double dy2 = f_origin_y + DispProg.rt_LayoutRelPos[DispProg.RunTime.UIndex2].Y + Line.Y[0];
                DispProg.TranslatePos(dx2, dy2, DispProg.rt_Head2RefData, ref dx2, ref dy2);

                dx = dx + DispProg.BiasKernel.X[DispProg.RunTime.Bias_Head_CR[0].X, DispProg.RunTime.Bias_Head_CR[0].Y];
                if (GDefine.GantryConfig == GDefine.EGantryConfig.XZ_YTABLE)
                    dy = dy - DispProg.BiasKernel.Y[DispProg.RunTime.Bias_Head_CR[0].X, DispProg.RunTime.Bias_Head_CR[0].Y];
                else
                    dy = dy + DispProg.BiasKernel.Y[DispProg.RunTime.Bias_Head_CR[0].X, DispProg.RunTime.Bias_Head_CR[0].Y];

                double X1 = dx;
                double Y1 = dy;
                double X2 = dx2;
                double Y2 = dy2;
                #endregion

                X1 = X1 + DispProg.OriginDrawOfst.X;
                Y1 = Y1 + DispProg.OriginDrawOfst.Y;
                X2 = X2 + DispProg.OriginDrawOfst.X;
                Y2 = Y2 + DispProg.OriginDrawOfst.Y;

                double X2_Ofst = X2 - X1;
                double Y2_Ofst = Y2 - Y1;

                TPos2 GXY = new TPos2(X1, Y1);
                TPos2 GX2Y2 = new TPos2(TaskDisp.Head2_DefPos.X, TaskDisp.Head2_DefPos.Y);
                #region Move To Pos
                switch (RunMode)
                {
                    case ERunMode.Normal:
                    case ERunMode.Dry:
                        {
                            if (!b_SyncHead2)
                            {
                                if (b_HeadRun[0])//(HeadNo == EHeadNo.Head1)
                                {
                                    GXY.X = GXY.X + TaskDisp.Head_Ofst[0].X;
                                    GXY.Y = GXY.Y + TaskDisp.Head_Ofst[0].Y;
                                }
                                if (b_HeadRun[1])//(HeadNo == EHeadNo.Head2)
                                {
                                    GXY.X = GXY.X + TaskDisp.Head_Ofst[1].X;
                                    GXY.Y = GXY.Y + TaskDisp.Head_Ofst[1].Y;
                                }
                            }
                            else
                            {
                                GXY.X = GXY.X + TaskDisp.Head_Ofst[0].X;
                                GXY.Y = GXY.Y + TaskDisp.Head_Ofst[0].Y;

                                GX2Y2.X = GX2Y2.X - TaskDisp.Head2_DefDistX + X2_Ofst + TaskDisp.Head2_XOffset;
                                GX2Y2.Y = GX2Y2.Y + Y2_Ofst + TaskDisp.Head2_YOffset;
                            }
                            break;
                        }
                    case ERunMode.Camera:
                    default:
                        {
                            break;
                        }
                }

                if (!TaskGantry.SetMotionParamGXY()) goto _Error;
                if (!TaskGantry.MoveAbsGXY(GXY.X, GXY.Y, false)) goto _Error;
                if (GDefine.GantryConfig == GDefine.EGantryConfig.XY_ZX2Y2_Z2)
                {
                    if (b_HeadRun[1])
                    {
                        if (!TaskGantry.SetMotionParamGX2Y2()) goto _Error;
                        if (!TaskGantry.MoveAbsGX2Y2(GX2Y2.X, GX2Y2.Y, false)) goto _Error;
                    }
                }
                if (GDefine.GantryConfig == GDefine.EGantryConfig.XY_ZX2Y2_Z2)
                    TaskGantry.WaitGX2Y2();
                TaskGantry.WaitGXY();
                #endregion

                double Z1 = 0;
                double Z2 = 0;
                #region Assign Z positions
                double dz = f_origin_z;
                dz = dz + TaskDisp.Head_Ofst[0].Z;
                double ZDiff = (TaskDisp.Head_ZSensor_RefPosZ[1] + TaskDisp.Head_Ofst[1].Z - (TaskDisp.Head_ZSensor_RefPosZ[0] + TaskDisp.Head_Ofst[0].Z));
                double dz2 = dz + ZDiff;

                Z1 = dz;
                Z2 = dz2;
                #endregion
                #region Update Z Offset
                Z1 = Z1 + TaskDisp.Z1Offset;
                Z2 = Z2 + TaskDisp.Z2Offset + TaskDisp.Head2_ZOffset;
                #endregion

                #region If ZPlane Valid, Update Z Values
                double LX1 = GXY.X - TaskDisp.Head_Ofst[0].X;
                double LY1 = GXY.Y - TaskDisp.Head_Ofst[0].Y;
                double LX2 = LX1 + (X2 - X1);
                double LY2 = LY1 + (Y2 - Y1);
                DispProg.UpdateZHeight(b_SyncHead2, LX1, LY1, LX2, LY2, ref Z1, ref Z2);
                #endregion
                #region move z to DispGap
                switch (RunMode)
                {
                    case ERunMode.Normal:
                    case ERunMode.Dry:
                        {

                            double sv = Model.DnStartV;
                            double dv = Model.DnSpeed;
                            double ac = Model.DnAccel;
                            if (!TaskGantry.SetMotionParamGZZ2(sv, dv, ac)) goto _Stop;
                            if (!DispProg.MoveZAbs(b_HeadRun[0], b_HeadRun[1], Z1 + Model.DispGap + Model.RetGap, Z2 + Model.DispGap + Model.RetGap)) return false;


                            break;
                        }
                    case ERunMode.Camera:
                    default:
                        {
                            break;
                        }
                }
                #endregion

                //double[] lineSpeed = new double[10] { Model.LineSpeed, Model.LineSpeed, Model.LineSpeed, Model.LineSpeed, Model.LineSpeed, Model.LineSpeed, Model.LineSpeed, Model.LineSpeed, Model.LineSpeed, Model.LineSpeed };
                double[] lineSpeed = Enumerable.Range(0, 100).Select(x => Model.LineSpeed).ToArray();

                #region Weighted
                double totalWeight = Line.DPara[1];
                double totalDispTime = DispProg.SP.DispTime[0];
                if (Model.DispVol > 0) totalDispTime = Model.DispVol;
                double totalDelayTime = 0;
                int lineIndex = 0;
                double totalLength = 0;
                if (Line.IPara[1] > 0)//enabled Weighted
                {
                    totalDispTime = totalWeight / TaskFlowRate.Value[0] * 1000;//ms
                    totalDispTime = totalDispTime * (1 + (DispProg.FlowRate.TimeCompensate / 100));


                    if (TaskFlowRate.Value[0] <= 0) throw new Exception("Invalid Flowrate. Perform Flowrate cal.");
                    if (totalWeight <= 0) throw new Exception("Weight value is invalid. Define weight value.");

                    //Calculate total length and delays of the group lines
                    lineIndex = 0;
                    for (int i = 0; i < 100; i++)
                    {
                        bool breakFor = false;
                        switch (Line.Index[i])
                        {
                            case (int)EGDispCmd.None:
                                breakFor = true;
                                break;
                            case (int)EGDispCmd.DOT:
                                if (DispProg.Pump_Type == TaskDisp.EPumpType.SP)
                                {
                                    //PPress On Lagging
                                    if (DispProg.SP.PulseOnDelay[0] > 0) totalDelayTime += DispProg.SP.PulseOnDelay[0];
                                }
                                if (Model.StartDelay > 0) totalDelayTime += Model.StartDelay;
                                if (Model.EndDelay > 0) totalDelayTime += Model.EndDelay;
                                if (DispProg.Pump_Type == TaskDisp.EPumpType.SP)
                                {
                                    //PPress Off Leading
                                    if (DispProg.SP.PulseOffDelay[0] < 0) totalDelayTime += DispProg.SP.PulseOffDelay[0];
                                }
                                if (totalDelayTime > totalDispTime) throw new Exception("Delay Time too long to achieve weight value. Decrease StartDelay and EndDelay time.");
                                breakFor = true;
                                break;
                            case (int)EGDispCmd.LINE_START:
                                totalLength = 0;
                                totalDelayTime = 0;
                                if (DispProg.Pump_Type == TaskDisp.EPumpType.SP)
                                {
                                    //PPress On Lagging
                                    if (DispProg.SP.PulseOnDelay[0] > 0) totalDelayTime += DispProg.SP.PulseOnDelay[0];
                                }
                                if (Model.StartDelay > 0) totalDelayTime += Model.StartDelay;
                                break;
                            case (int)EGDispCmd.LINE_PASS:
                                {
                                    double lineLength = Math.Sqrt(Math.Pow(Line.X[i], 2) + Math.Pow(Line.Y[i], 2));
                                    totalLength += lineLength;
                                }
                                break;
                            case (int)EGDispCmd.LINE_END:
                                {
                                    double lineLength = Math.Sqrt(Math.Pow(Line.X[i], 2) + Math.Pow(Line.Y[i], 2));
                                    totalLength += lineLength;

                                    if (Model.EndDelay > 0) totalDelayTime += Model.EndDelay;
                                    if (DispProg.Pump_Type == TaskDisp.EPumpType.SP)
                                    {
                                        //PPress Off Leading
                                        if (DispProg.SP.PulseOffDelay[0] < 0) totalDelayTime += DispProg.SP.PulseOffDelay[0];
                                    }

                                    double lineTime = totalDispTime - totalDelayTime;
                                    if (lineTime <= 0) throw new Exception("Delay Time too long to achieve weight value. Decrease StartDelay and EndDelay time.");

                                    //////set u = 0, 


                                    //dT = totalLength - total length of the lines
                                    double dT = totalLength;
                                    //tT = totalTime - total time for total lines move
                                    double tT = lineTime / 1000;//unit seconds
                                    //a = accel
                                    double a = Model.LineAccel;
                                    //
                                    //set u=0
                                    //
                                    //Triangle profile
                                    //Peak v, vP = a * (tT/2)
                                    double vP = a * tT / 2;
                                    //Triangle distance
                                    double dA = 0.5 * vP * tT;

                                    if (dA < dT) throw new Exception("Line Accel cannot achive line distance. Increase Line Accel.");

                                    //Excess distance, dE
                                    double dE = dA - dT;

                                    //dE = 1 / 2 * vE * tE, tE = tT / vP * vE
                                    //dE = 1 / 2 * vE * tT / vP * vE
                                    //vE2* tT/ vP = 2 * dE
                                    //vE2 = 2 * dE * vP / tT
                                    //vE = Sqrt(2 * dE * vP / tT)
                                    //Excess speed, vE
                                    double vE = Math.Sqrt(2 * dE * vP / tT);
                                    //Constant Speed
                                    double v = vP - vE;

                                    lineSpeed[lineIndex] = v;
                                    if (GDefine.LogLevel == 1) Log.AddToLog("Line " + lineIndex.ToString() + " " + v.ToString("f3") + "mm/s");

                                    if (lineSpeed[lineIndex] > 100) throw new Exception("Line Speed over 100mm/s. Run Aborted.");

                                    lineIndex++;
                                }
                                break;
                        }
                        if (breakFor) break;
                    }
                }
                #endregion

                int t = GDefine.GetTickCount();
                #region Prepare Paths
                CControl2.TAxis[] Axis = new CControl2.TAxis[] { TaskGantry.GXAxis, TaskGantry.GYAxis, TaskGantry.GZAxis };
                CommonControl.P1245.PathFree(Axis);
                CommonControl.P1245.SetAccel(Axis, Model.LineAccel);
                bool b_Blend = false;
                #endregion

                double relX = 0;
                double relY = 0;
                double relGap = Model.RetGap;
                lineIndex = 0;
                for (int i = 0; i < 100; i++)
                {
                    bool breakFor = false;
                    switch (Line.Index[i])
                    {
                        case (int)EGDispCmd.None:
                             breakFor = true;
                            break;
                        case (int)EGDispCmd.DOT:
                        case (int)EGDispCmd.DOT_START:
                            if (i > 0)
                                CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, Model.LineSpeed, 0, new double[3] { Line.X[i], Line.Y[i], 0 }, null);
                            #region Path Move to Disp Gap
                            switch (RunMode)
                            {
                                case ERunMode.Normal:
                                case ERunMode.Dry:
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, Model.DnSpeed, Model.DnStartV, new double[3] { 0, 0, -relGap }, null);
                                    relGap = 0;
                                    break;
                            }
                            if (Model.DnWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.DnWait, 0, null, null);
                            #endregion
                            #region Path Pump On
                            switch (RunMode)
                            {
                                case ERunMode.Normal:
                                    {
                                        switch (DispProg.Pump_Type)
                                        {
                                            case TaskDisp.EPumpType.SP:
                                                if (Line.U[i] == 0) TaskDisp.SP.SP_AddOnPaths(Axis);
                                                if (Model.StartDelay > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.StartDelay, 0, null, null);
                                                break;
                                            case TaskDisp.EPumpType.TP:
                                                if (Line.U[i] == 0) TaskDisp.TP.AddOnPaths(Axis);
                                                if (Model.StartDelay > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.StartDelay, 0, null, null);
                                                break;
                                            default:
                                                CControl2.TOutput[] Output = new CControl2.TOutput[] { };
                                                DispProg.Outputs(b_HeadRun, ref Output);
                                                if (Line.U[i] == 0) CommonControl.P1245.PathAddDO(Axis, Output, RunMode == ERunMode.Normal);
                                                if (Model.StartDelay > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.StartDelay, 0, null, null);
                                                break;
                                        }
                                    }
                                    break;
                            }
                            #endregion

                            double nettDotTime = totalDispTime - totalDelayTime;

                            if (nettDotTime > 0)
                                CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, nettDotTime, 0, null, null);

                            #region Path Pump Off
                            switch (RunMode)
                            {
                                case ERunMode.Normal:
                                case ERunMode.Dry:
                                    {
                                        switch (DispProg.Pump_Type)
                                        {
                                            case TaskDisp.EPumpType.SP:
                                                TaskDisp.SP.SP_AddOffPaths(Axis);
                                                break;
                                            case TaskDisp.EPumpType.TP:
                                                TaskDisp.TP.AddOffPaths(Axis);
                                                break;
                                            default:
                                                CControl2.TOutput[] Output = new CControl2.TOutput[] { };
                                                DispProg.Outputs(b_HeadRun, ref Output);
                                                CommonControl.P1245.PathAddDO(Axis, Output, false);
                                                if (b_HeadRun[0] && RunMode == ERunMode.Normal) DispProg.Stats.DispCount_Inc(0);
                                                if (b_HeadRun[1] && RunMode == ERunMode.Normal) DispProg.Stats.DispCount_Inc(1);
                                                break;
                                        }
                                    }
                                    break;
                            }
                            if (b_HeadRun[0] && RunMode == ERunMode.Normal) DispProg.Stats.DispCount_Inc(0);
                            if (b_HeadRun[1] && RunMode == ERunMode.Normal) DispProg.Stats.DispCount_Inc(1);
                            #endregion
                            if (Model.PostWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.PostWait, 0, null, null);
                            #region Path Retract and Up
                            switch (RunMode)
                            {
                                case ERunMode.Normal:
                                case ERunMode.Dry:
                                    {
                                        if (Model.RetGap > 0)
                                        {
                                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, Model.RetSpeed, Model.RetStartV, new double[3] { 0, 0, Model.RetGap }, null);
                                            relGap += Model.RetGap;
                                            if (Model.RetWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.RetWait, 0, null, null);
                                        }
                                        if (Line.Index[i] == (int)EGDispCmd.DOT && Model.UpGap > 0)
                                        {
                                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DDirect, b_Blend, Model.UpSpeed, Model.UpStartV, new double[3] { 0, 0, Model.UpGap }, null);
                                            relGap += Model.UpGap;
                                            if (Model.UpWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.UpWait, 0, null, null);
                                        }
                                        break;
                                    }
                                case ERunMode.Camera:
                                default:
                                    {
                                        if (Model.RetWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.RetWait, 0, null, null);
                                        if (Line.Index[i] == (int)EGDispCmd.DOT && Model.UpWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.UpWait, 0, null, null);
                                        break;
                                    }
                            }
                            #endregion
                            break;
                        case (int)EGDispCmd.DOT_END:
                            #region Path Retract and Up
                            switch (RunMode)
                            {
                                case ERunMode.Normal:
                                case ERunMode.Dry:
                                    {
                                        //if (Model.UpGap > 0)
                                        {
                                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, Model.LineSpeed, 0, new double[3] { Line.X[i], Line.Y[i], 0 }, null);

                                            if (Model.UpGap > 0)
                                            {
                                                CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DDirect, b_Blend, Model.UpSpeed, Model.UpStartV, new double[3] { 0, 0, Model.UpGap }, null);
                                                relGap += Model.UpGap;
                                                if (Model.UpWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.UpWait, 0, null, null);
                                            }
                                        }
                                        break;
                                    }
                                case ERunMode.Camera:
                                default:
                                    {
                                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, Model.UpSpeed, 0, new double[3] { Line.X[i], Line.Y[i], 0 }, null);
                                        if (Model.UpWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.UpWait, 0, null, null);
                                        break;
                                    }
                            }
                            #endregion
                            break;
                        case (int)EGDispCmd.LINE_START:
                            if (i > 0)
                            {
                                CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, TaskGantry.GXAxis.Para.FastV, 0, new double[3] { Line.X[i] - relX, Line.Y[i] - relY, 0 }, null);
                                relX = 0;
                                relY = 0;
                            }
                            #region Path Move to Disp Gap
                            switch (RunMode)
                            {
                                case ERunMode.Normal:
                                case ERunMode.Dry:
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, Model.DnSpeed, Model.DnStartV, new double[3] { 0, 0, -relGap }, null);
                                    relGap = 0;
                                    break;
                            }
                            if (Model.DnWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.DnWait, 0, null, null);
                            #endregion
                            #region Path Pump On
                            switch (RunMode)
                            {
                                case ERunMode.Normal:
                                    {
                                        switch (DispProg.Pump_Type)
                                        {
                                            case TaskDisp.EPumpType.SP:
                                                if (Line.U[i] == 0) TaskDisp.SP.SP_AddOnPaths(Axis);
                                                if (Model.StartDelay > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.StartDelay, 0, null, null);
                                                break;
                                            case TaskDisp.EPumpType.TP:
                                                if (Line.U[i] == 0) TaskDisp.TP.AddOnPaths(Axis);
                                                if (Model.StartDelay > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.StartDelay, 0, null, null);
                                                break;
                                            default:
                                                CControl2.TOutput[] Output = new CControl2.TOutput[] { };
                                                DispProg.Outputs(b_HeadRun, ref Output);
                                                if (Line.U[i] == 0) CommonControl.P1245.PathAddDO(Axis, Output, RunMode == ERunMode.Normal);
                                                if (Model.StartDelay > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.StartDelay, 0, null, null);
                                                break;
                                        }
                                    }
                                    break;
                            }
                            #endregion
                            break;
                        case (int)EGDispCmd.LINE_PASS:
                            #region Path Pass
                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, lineSpeed[lineIndex], 0, new double[3] { Line.X[i], Line.Y[i], 0 }, null);
                            #endregion
                            break;
                        case (int)EGDispCmd.LINE_END:
                            #region Path Pass
                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, lineSpeed[lineIndex], 0, new double[3] { Line.X[i], Line.Y[i], 0 }, null);
                            #endregion
                            #region Path Pump Off
                            switch (RunMode)
                            {
                                case ERunMode.Normal:
                                case ERunMode.Dry:
                                    {
                                        switch (DispProg.Pump_Type)
                                        {
                                            case TaskDisp.EPumpType.SP:
                                                TaskDisp.SP.SP_AddOffPaths(Axis);
                                                break;
                                            case TaskDisp.EPumpType.TP:
                                                TaskDisp.TP.AddOffPaths(Axis);
                                                break;
                                            default:
                                                CControl2.TOutput[] Output = new CControl2.TOutput[] { };
                                                DispProg.Outputs(b_HeadRun, ref Output);
                                                CommonControl.P1245.PathAddDO(Axis, Output, false);
                                                if (b_HeadRun[0] && RunMode == ERunMode.Normal) DispProg.Stats.DispCount_Inc(0);
                                                if (b_HeadRun[1] && RunMode == ERunMode.Normal) DispProg.Stats.DispCount_Inc(1);
                                                break;
                                        }
                                    }
                                    break;
                            }
                            if (b_HeadRun[0] && RunMode == ERunMode.Normal) DispProg.Stats.DispCount_Inc(0);
                            if (b_HeadRun[1] && RunMode == ERunMode.Normal) DispProg.Stats.DispCount_Inc(1);
                            #endregion
                            #region Path CutTail
                            double lineLength = Math.Sqrt(Math.Pow(Line.X[i], 2) + Math.Pow(Line.Y[i], 2));
                            double extRelX = Line.X[i] * Line.DPara[10] / lineLength;
                            double extRelY = Line.Y[i] * Line.DPara[10] / lineLength;
                            double cutTailSpeed = Line.DPara[11];
                            double cutTailSSpeed = Math.Min(Model.LineStartV, cutTailSpeed);
                            double cutTailHeight = Line.DPara[12];
                            ECutTailType cutTailType = ECutTailType.None;
                            try { cutTailType = (ECutTailType)Line.DPara[13]; } catch { };

                            switch (cutTailType)
                            {
                                case ECutTailType.None:
                                    break;
                                case ECutTailType.Fwd:
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[3] { extRelX, extRelY, cutTailHeight }, null);
                                    relX = extRelX;
                                    relY = extRelY;
                                    break;
                                case ECutTailType.Bwd:
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[3] { -extRelX, -extRelY, cutTailHeight }, null);
                                    relX = -extRelX;
                                    relY = -extRelY;
                                    break;
                                case ECutTailType.SqFwd:
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[3] { 0, 0, cutTailHeight }, null);
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[3] { extRelX, extRelY, 0 }, null);
                                    relX = extRelX;
                                    relY = extRelY;
                                    break;
                                case ECutTailType.SqBwd:
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[3] { 0, 0, cutTailHeight }, null);
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[3] { -extRelX, -extRelY, 0 }, null);
                                    relX = -extRelX;
                                    relY = -extRelY;
                                    break;
                                case ECutTailType.Rev:
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[3] { extRelX, extRelY, 0 }, null);
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[3] { -extRelX, -extRelY, cutTailHeight }, null);
                                    break;
                                case ECutTailType.SqRev:
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[3] { extRelX, extRelY, 0 }, null);
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[3] { 0, 0, cutTailHeight }, null);
                                    CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[3] { -extRelX, -extRelY, 0 }, null);
                                    break;
                            }
                            relGap += cutTailHeight;
                            lineIndex++;
                            #endregion
                            #region Path Retract and Up
                            switch (RunMode)
                            {
                                case ERunMode.Normal:
                                case ERunMode.Dry:
                                    {
                                        if (Model.RetGap > 0)
                                        {
                                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, Model.RetSpeed, Model.RetStartV, new double[3] { 0, 0, Model.RetGap }, null);
                                            relGap += Model.RetGap;
                                            if (Model.RetWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.RetWait, 0, null, null);
                                        }
                                        if (Model.UpGap > 0)
                                        {
                                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, b_Blend, Model.UpSpeed, Model.UpStartV, new double[3] { 0, 0, Model.UpGap }, null);
                                            relGap += Model.UpGap;
                                            if (Model.UpWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.UpWait, 0, null, null);
                                        }
                                        break;
                                    }
                                case ERunMode.Camera:
                                default:
                                    {
                                        if (Model.RetWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.RetWait, 0, null, null);
                                        if (Model.UpWait > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, b_Blend, Model.UpWait, 0, null, null);
                                        break;
                                    }
                            }
                            #endregion
                            break;
                    }
                    if (breakFor) break;
                }

                #region Move Paths
                CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, TaskGantry.GXAxis.Para.FastV, 0, new double[3] { 0.001, 0.001, 0 }, null);

                uint index = 0, curr = 0, remain = 0;
                CommonControl.P1245.PathInfo(Axis, ref index, ref curr, ref remain);
                if (remain > 0) CommonControl.P1245.PathEnd(Axis);
                CommonControl.P1245.PathMove(Axis);
                while (true)
                {
                    if (!CommonControl.P1245.AxisBusy(Axis)) break;
                }
                if (GDefine.LogLevel == 1) Log.AddToLog("Line Time " + (GDefine.GetTickCount() - t).ToString("f3") + "ms");
                #endregion}
            }
            catch (Exception Ex)
            {
                GDefine.Status = EStatus.ErrorInit;
                TaskDisp.TrigOff(true, true);
                EMsg = EMsg + (char)13 + Ex.Message.ToString();
                throw new Exception(EMsg);
            }
        _End:
            GDefine.Status = EStatus.Ready;
            return true;
        _Stop:
            GDefine.Status = EStatus.Stop;
            return false;
        _Error:
            GDefine.Status = EStatus.ErrorInit;
            return false;
        }
    }

    internal class NetDisp
    {
        public static bool Net_Line(DispProg.TLine Line, ERunMode RunMode, double f_origin_x, double f_origin_y, double f_origin_z)
        {
            GDefine.Status = EStatus.Busy;

            try
            {
                TModelPara Model = new TModelPara(DispProg.ModelList, Line.IPara[0]);
                bool disp = (Line.IPara[2] > 0);

                EVHType vhType = EVHType.Hort;//0=Horizontal, 1=Vertical
                try { vhType = (EVHType)Line.IPara[3]; } catch { };

                ELineType lineType = ELineType.Cont;
                try { lineType = (ELineType)Line.IPara[4]; } catch { };

                double leadLength = Line.DPara[0];
                double lagLength = Line.DPara[0];

                bool[] bHeadRun = new bool[2] { false, false };
                bool bHead2IsValid = false;
                bool bSyncHead2 = false;
                if (!DispProg.SelectHead(Line, ref bHeadRun, ref bHead2IsValid, ref bSyncHead2)) goto _End;

                TLayout layout = new TLayout();
                layout.Copy(DispProg.rt_Layouts[DispProg.rt_LayoutID]);

                Point currentUnitCR = new Point(0, 0);
                layout.UnitNoGetRC(DispProg.RunTime.UIndex, ref currentUnitCR);
                Point currentClusterCR = new Point(0, 0);
                currentClusterCR.X = currentUnitCR.X / layout.UColCount;
                currentClusterCR.Y = currentUnitCR.Y / layout.URowCount;

                if (!DispProg.SetPumpParameters(Model, disp, bHeadRun)) goto _Stop;

                #region Move GZ2 Up if invalid
                if (GDefine.GantryConfig == GDefine.EGantryConfig.XY_ZX2Y2_Z2 && !bHead2IsValid)
                {
                    switch (RunMode)
                    {
                        case ERunMode.Normal:
                        case ERunMode.Dry:
                            if (!TaskDisp.TaskMoveGZ2Up()) return false;
                            break;
                    }
                }
                #endregion

                #region assign and xy translate position
                TPos2[] absStart = new TPos2[]
                {
                new TPos2(f_origin_x + DispProg.rt_LayoutRelPos[DispProg.RunTime.UIndex].X + Line.X[0], f_origin_y + DispProg.rt_LayoutRelPos[DispProg.RunTime.UIndex].Y + Line.Y[0]),
                new TPos2(f_origin_x + DispProg.rt_LayoutRelPos[DispProg.RunTime.UIndex2].X + Line.X[0], f_origin_y + DispProg.rt_LayoutRelPos[DispProg.RunTime.UIndex2].Y + Line.Y[0])
                };
                DispProg.TranslatePos(absStart[0].X, absStart[0].Y, DispProg.rt_Head1RefData, ref absStart[0].X, ref absStart[0].Y);
                DispProg.TranslatePos(absStart[1].X, absStart[1].Y, DispProg.rt_Head1RefData, ref absStart[1].X, ref absStart[1].Y);
                #endregion

                #region Calculate the End positions
                Point firstCR = new Point(0, 0);
                Point lastCR = new Point(0, 0);
                int lastUnitNo = 0;//Last UnitNo of Hort/Vert line 
                if (vhType == EVHType.Hort)
                {
                    layout.UnitNoGetRC(DispProg.RunTime.UIndex, ref firstCR);
                    lastCR = new Point((currentClusterCR.X * layout.UColCount) + layout.UColCount - 1, firstCR.Y);
                    layout.RCGetUnitNo(ref lastUnitNo, lastCR.X, lastCR.Y);//Get the last unit number of the current row.
                }
                else
                {
                    layout.UnitNoGetRC(DispProg.RunTime.UIndex, ref firstCR);
                    lastCR = new Point(firstCR.X, (currentClusterCR.Y * layout.URowCount) + layout.URowCount - 1);
                    layout.RCGetUnitNo(ref lastUnitNo, lastCR.X, lastCR.Y);//Get the last unit number of the current col.
                }
                TPos2 absEnd = new TPos2(f_origin_x + DispProg.rt_LayoutRelPos[lastUnitNo].X + Line.X[0], f_origin_y + DispProg.rt_LayoutRelPos[lastUnitNo].Y + Line.Y[0]);
                DispProg.TranslatePos(absEnd.X, absEnd.Y, DispProg.rt_Head1RefData, ref absEnd.X, ref absEnd.Y);
                #endregion

                bool reverse = Line.IPara[5] > 0;
                if (reverse)
                {
                    if ((vhType == EVHType.Hort && firstCR.Y % 2 != 0) || (vhType == EVHType.Vert && firstCR.X % 2 != 0))
                    {
                        TPos2 temp = new TPos2(absStart[0]);
                        absStart[0] = new TPos2(absEnd);
                        absEnd = new TPos2(temp);
                    }
                }

                #region Calculate the start Z positions
                double[] absZ = new double[] { 0, 0 };
                absZ[0] = f_origin_z + TaskDisp.Head_Ofst[0].Z; //Assign Z positions
                absZ[1] = absZ[0] + (TaskDisp.Head_ZSensor_RefPosZ[1] - TaskDisp.Head_ZSensor_RefPosZ[0]); //Update ZPlane if valid Z values
                DispProg.UpdateZHeight(bSyncHead2, absStart[0].X, absStart[0].Y, absStart[1].X, absStart[1].Y, ref absZ[0], ref absZ[1]);
                double[] zRetGapPos = new double[] { Math.Min(absZ[0] + Model.DispGap + Model.RetGap, TaskDisp.ZDefPos), Math.Min(absZ[1] + Model.DispGap + Model.RetGap, TaskDisp.ZDefPos) };

                double[] absEndZ = new double[] { absZ[0], absZ[1] };
                DispProg.UpdateZHeight(bSyncHead2, absEnd.X, absEnd.Y, absEnd.X, absEnd.Y, ref absEndZ[0], ref absEndZ[1]);//Head2 Z follow Head1
                #endregion

                //Calculate the relative line end pos
                TPos2 relLineEndXY = new TPos2(absEnd.X - absStart[0].X, absEnd.Y - absStart[0].Y);
                double relEndZ = absEndZ[0] - absZ[0];

                //Calculate the lead lag relative pos
                double lineLength = Math.Sqrt(Math.Pow(relLineEndXY.X, 2) + Math.Pow(relLineEndXY.Y, 2));
                TPos2 relLeadXY = new TPos2(leadLength / lineLength * relLineEndXY.X, leadLength / lineLength * relLineEndXY.Y);
                TPos2 relLagXY = new TPos2(lagLength / lineLength * relLineEndXY.X, lagLength / lineLength * relLineEndXY.Y);

                //lastAbsXY = new TPos2[] { new TPos2(absXY[0]), new TPos2(absXY[1]) };

                #region Move abs Start Pos + lead length, move head2 to position
                if (!TaskGantry.SetMotionParamGXY()) goto _Error;
                TPos2 GXY = new TPos2(absStart[0].X - relLeadXY.X, absStart[0].Y - relLeadXY.Y);
                if (RunMode == ERunMode.Normal || RunMode == ERunMode.Dry)
                {
                    GXY.X += TaskDisp.Head_Ofst[0].X;
                    GXY.Y += TaskDisp.Head_Ofst[0].Y;
                }
                if (!TaskGantry.MoveAbsGXY(GXY.X, GXY.Y, false)) goto _Error;
                TPos2 GX2Y2 = new TPos2(TaskDisp.Head2_DefPos.X, TaskDisp.Head2_DefPos.Y);
                if (GDefine.GantryConfig == GDefine.EGantryConfig.XY_ZX2Y2_Z2)
                {
                    if (bHeadRun[1])
                    {
                        GX2Y2.X = GX2Y2.X - TaskDisp.Head2_DefDistX + (absStart[1].X - absStart[0].X) + TaskDisp.Head2_XOffset;
                        GX2Y2.Y = GX2Y2.Y + (absStart[1].Y - absStart[0].Y) + TaskDisp.Head2_YOffset;

                        if (!TaskGantry.SetMotionParamGX2Y2()) goto _Error;
                        if (!TaskGantry.MoveAbsGX2Y2(GX2Y2.X, GX2Y2.Y, false)) goto _Error;
                    }
                    TaskGantry.WaitGX2Y2();
                }
                TaskGantry.WaitGXY();
                #endregion

                //Move to abs RetractGap
                switch (RunMode)
                {
                    case ERunMode.Normal:
                    case ERunMode.Dry:
                        {
                            if (!TaskGantry.SetMotionParamGZZ2(Model.DnStartV, Model.DnSpeed, Model.DnAccel)) return false;
                            if (!DispProg.MoveZAbs(bHeadRun[0], bHeadRun[1], zRetGapPos[0], zRetGapPos[1])) return false;
                            //return false;
                            break;
                        }
                    case ERunMode.Camera:
                    default:
                        {
                            break;
                        }
                }

                double[] GZ = new double[] { Math.Min(absZ[0] + Model.DispGap, TaskDisp.ZDefPos), Math.Min(absZ[1] + Model.DispGap + TaskDisp.Head2_ZOffset, TaskDisp.ZDefPos) };//include H2 ZOffset

                //Move to abs DispGap
                switch (RunMode)
                {
                    case ERunMode.Normal:
                    case ERunMode.Dry:
                        {

                            double sv = Model.DnStartV;
                            double dv = Model.DnSpeed;
                            double ac = Model.DnAccel;
                            if (!TaskGantry.SetMotionParamGZZ2(sv, dv, ac)) goto _Stop;
                            if (!DispProg.MoveZAbs(bHeadRun[0], bHeadRun[1], GZ[0], GZ[1])) return false;

                            break;
                        }
                    case ERunMode.Camera:
                    default:
                        {
                            break;
                        }
                }

                #region Dn Wait
                int t = GDefine.GetTickCount() + (int)Model.DnWait;
                while (GDefine.GetTickCount() < t)
                {
                    if (Model.DnWait > 75) Thread.Sleep(1);
                }
                #endregion

                #region Start Disp and StartDelay
                CControl2.TAxis[] Axis = new CControl2.TAxis[] { TaskGantry.GXAxis, TaskGantry.GYAxis, TaskGantry.GZAxis, TaskGantry.GZ2Axis };
                CommonControl.P1245.PathFree(Axis);
                CControl2.TOutput[] Output = null;
                DispProg.Outputs(bHeadRun, ref Output);
                CommonControl.P1245.PathAddDO(Axis, Output, disp && RunMode == ERunMode.Normal);
                CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, true, Model.StartDelay, 0, null, null);
                #endregion

                double LineSpeed = Model.LineSpeed;
                CommonControl.P1245.SetAccel(Axis, Model.LineAccel);

                if (lineType == ELineType.Cont)
                {
                    if (RunMode == ERunMode.Normal || RunMode == ERunMode.Dry)
                    {
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLeadXY.X, relLeadXY.Y, 0, 0 }, null);
                        CommonControl.P1245.PathAddDO(Axis, Output, disp && RunMode == ERunMode.Normal);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLineEndXY.X, relLineEndXY.Y, relEndZ, relEndZ }, null);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLagXY.X, relLagXY.Y, 0, 0 }, null);
                        CommonControl.P1245.PathAddDO(Axis, Output, false);
                    }
                    else
                    {
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLeadXY.X, relLeadXY.Y, 0, 0 }, null);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLineEndXY.X, relLineEndXY.Y, 0, 0 }, null);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLagXY.X, relLagXY.Y, 0, 0 }, null);
                    }
                }
                else//lineType == ELineType.Dash
                {
                    double delayOnDist = Line.DPara[3];
                    double earlyOffDist = Line.DPara[4];
                    double junctionLift = Line.DPara[5];

                    #region Line positions
                    double relXYLength = Math.Sqrt(Math.Pow(relLineEndXY.X, 2) + Math.Pow(relLineEndXY.Y, 2));
                    double unitPitchDist = Math.Sqrt(Math.Pow(layout.URowPX, 2) + Math.Pow(layout.URowPY, 2));

                    //Calc DelayOn rel position
                    TPos2 relDelayOnXY = new TPos2(relLineEndXY.X * delayOnDist / relXYLength, relLineEndXY.Y * delayOnDist / relXYLength);
                    double relDelayOnZ = relEndZ * delayOnDist / relXYLength;

                    //Calc EarlyOff rel position
                    TPos2 relEarlyOffXY = new TPos2(relLineEndXY.X * earlyOffDist / relXYLength, relLineEndXY.Y * earlyOffDist / relXYLength);
                    double relEarlyOffZ = relEndZ * earlyOffDist / relXYLength;

                    //Calc Line rel position
                    double dispDist = unitPitchDist - delayOnDist - earlyOffDist;
                    TPos2 relDispXY = new TPos2(relLineEndXY.X * dispDist / relXYLength, relLineEndXY.Y * dispDist / relXYLength);
                    double dispDistZ = unitPitchDist - delayOnDist - earlyOffDist;
                    double relDispDistZ = relEndZ * dispDist / relXYLength;
                    #endregion

                    //Calc EarlyOffDist in lead line
                    TPos2 relLead2XY = new TPos2(relLeadXY);
                    double lead2Length = leadLength - earlyOffDist;
                    if (lead2Length > 0) relLead2XY = new TPos2(relLeadXY.X * lead2Length / leadLength, relLeadXY.Y * lead2Length / leadLength);

                    //Calc DelayOnDist in lead line
                    TPos2 relLag2XY = new TPos2(relLagXY);
                    double lag2Length = lagLength - delayOnDist;
                    if (lag2Length > 0) relLag2XY = new TPos2(relLagXY.X * lag2Length / lagLength, relLagXY.Y * lag2Length / lagLength);

                    int dashCount = (vhType == EVHType.Hort ? layout.UColCount : layout.URowCount);

                    if (RunMode == ERunMode.Normal || RunMode == ERunMode.Dry)
                    {
                        //CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLeadXY.X, relLeadXY.Y, junctionLift, junctionLift }, null);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLead2XY.X, relLead2XY.Y, 0, 0 }, null);
                        if (lead2Length > 0)
                        {
                            CommonControl.P1245.PathAddDO(Axis, Output, false);
                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relEarlyOffXY.X, relEarlyOffXY.Y, junctionLift, junctionLift }, null);
                        }
                    }
                    else
                    {
                        //CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLeadXY.X, relLeadXY.Y, 0, 0 }, null);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLead2XY.X, relLead2XY.Y, 0, 0 }, null);
                        if (lead2Length > 0)
                        {
                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relEarlyOffXY.X, relEarlyOffXY.Y, 0, 0 }, null);
                        }
                    }

                    for (int r = 0; r < dashCount - 1; r++)
                    {
                        if (RunMode == ERunMode.Normal || RunMode == ERunMode.Dry)
                        {
                            CommonControl.P1245.PathAddDO(Axis, Output, false);
                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relDelayOnXY.X, relDelayOnXY.Y, relDelayOnZ - junctionLift, relDelayOnZ - junctionLift }, null);
                            CommonControl.P1245.PathAddDO(Axis, Output, disp && RunMode == ERunMode.Normal);
                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relDispXY.X, relDispXY.Y, relDispDistZ, relDispDistZ }, null);
                            CommonControl.P1245.PathAddDO(Axis, Output, false);
                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relEarlyOffXY.X, relEarlyOffXY.Y, relEarlyOffZ + junctionLift, relEarlyOffZ + junctionLift }, null);
                        }
                        else
                        {
                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relDelayOnXY.X, relDelayOnXY.Y, 0, 0 }, null);
                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relDispXY.X, relDispXY.Y, 0, 0 }, null);
                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relEarlyOffXY.X, relEarlyOffXY.Y, 0, 0 }, null);
                        }
                    }

                    if (RunMode == ERunMode.Normal || RunMode == ERunMode.Dry)
                    {
                        //CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLagXY.X, relLagXY.Y, 0, 0 }, null);
                        if (lag2Length > 0)
                            CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relDelayOnXY.X, relDelayOnXY.Y, -junctionLift, -junctionLift }, null);
                        CommonControl.P1245.PathAddDO(Axis, Output, disp && RunMode == ERunMode.Normal);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLag2XY.X, relLag2XY.Y, 0, 0 }, null);
                        CommonControl.P1245.PathAddDO(Axis, Output, false);
                    }
                    else
                    {
                        if (lag2Length > 0) CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relDelayOnXY.X, relDelayOnXY.Y, 0, 0 }, null);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel4DDirect, false, LineSpeed, LineSpeed, new double[4] { relLag2XY.X, relLag2XY.Y, 0, 0 }, null);
                    }
                }

                CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.GPDELAY, true, Model.PostWait, 0, null, null);

                #region Path CutTail
                double priorLineLength = Math.Sqrt(Math.Pow(relLagXY.X, 2) + Math.Pow(relLagXY.Y, 2));
                if (priorLineLength == 0) goto _SkipCutTail;
                double extRelX = relLagXY.X * Line.DPara[10] / priorLineLength;
                double extRelY = relLagXY.Y * Line.DPara[10] / priorLineLength;
                double cutTailSpeed = Line.DPara[11];
                double cutTailSSpeed = Math.Min(Model.LineStartV, cutTailSpeed);
                double cutTailHeight = (RunMode == ERunMode.Normal || RunMode == ERunMode.Dry) ? Line.DPara[12] : 0;
                ECutTailType cutTailType = ECutTailType.None;
                try { cutTailType = (ECutTailType)Line.DPara[13]; } catch { };

                bool b_Blend = false;

                switch (cutTailType)
                {
                    case ECutTailType.None:
                        break;
                    case ECutTailType.Fwd:
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[4] { extRelX, extRelY, cutTailHeight, cutTailHeight }, null);
                        break;
                    case ECutTailType.Bwd:
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[4] { -extRelX, -extRelY, cutTailHeight, cutTailHeight }, null);
                        break;
                    case ECutTailType.SqFwd:
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[4] { 0, 0, cutTailHeight, cutTailHeight }, null);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[4] { extRelX, extRelY, 0, 0 }, null);
                        break;
                    case ECutTailType.SqBwd:
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[4] { 0, 0, cutTailHeight, cutTailHeight }, null);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[4] { -extRelX, -extRelY, 0, 0 }, null);
                        break;
                    case ECutTailType.Rev:
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[4] { extRelX, extRelY, 0, 0 }, null);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[4] { -extRelX, -extRelY, cutTailHeight, cutTailHeight }, null);
                        break;
                    case ECutTailType.SqRev:
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[4] { extRelX, extRelY, 0, 0 }, null);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[4] { 0, 0, cutTailHeight, cutTailHeight }, null);
                        CommonControl.P1245.PathAddCmd(Axis, CControl2.EPath_MoveCmd.Rel3DLine, b_Blend, cutTailSpeed, cutTailSSpeed, new double[4] { -extRelX, -extRelY, 0, 0 }, null);
                        break;
                }
            _SkipCutTail:
                #endregion

                CommonControl.P1245.PathEnd(Axis);
                CommonControl.P1245.PathMove(Axis);

                while (true)
                {
                    if (!CommonControl.P1245.AxisBusy(Axis)) break;
                }

                #region Move ZRetGap, ZUpGap and ZPanelGap
                switch (RunMode)
                {
                    case ERunMode.Normal:
                    case ERunMode.Dry:
                        {
                            if (Model.RetGap != 0)
                            {
                                #region Move Ret
                                if (!TaskGantry.SetMotionParamGZZ2(Model.RetStartV, Model.RetSpeed, Model.RetAccel)) return false;
                                if (!DispProg.MoveRelZ(bHeadRun[0], bHeadRun[1], Model.RetGap, Model.RetGap)) return false;
                                #endregion
                                #region Ret Wait
                                if (Model.RetWait > 0)
                                {
                                    t = GDefine.GetTickCount() + Model.RetWait;
                                    while (GDefine.GetTickCount() < t)
                                    {
                                        TaskDisp.Thread_CheckIsFilling_Run(bHeadRun[0], bHeadRun[1]);
                                    }
                                }
                                #endregion
                            }
                            if (Model.UpGap != 0)
                            {
                                #region Move Up
                                if (!TaskGantry.SetMotionParamGZZ2(Model.UpStartV, Model.UpSpeed, Model.UpAccel)) return false;
                                if (!DispProg.MoveRelZ(bHeadRun[0], bHeadRun[1], Model.UpGap, Model.UpGap)) return false;
                                #endregion
                                #region Up Wait
                                t = GDefine.GetTickCount() + Model.UpWait;
                                while (GDefine.GetTickCount() < t)
                                {
                                    TaskDisp.Thread_CheckIsFilling_Run(bHeadRun[0], bHeadRun[1]);
                                }
                                #endregion
                            }
                            break;
                        }
                    case ERunMode.Camera:
                        {
                            break;
                        }
                }
                #endregion

                if (DispProg.Options_EnableProcessLog)
                {
                    double gz1 = TaskGantry.EncoderPos(TaskGantry.GZAxis);
                    string str = $"{Line.Cmd}\t";
                    str += $"DispGap={Model.DispGap:f3}\t";
                    str += $"C,R={DispProg.RunTime.Head_CR[0].X},{DispProg.RunTime.Head_CR[0].Y}\t";
                    str += $"X,Y,Z={GXY.X:f3},{GXY.Y:f3},{gz1:f3} XE,YE,ZE ={ GXY.X + relLineEndXY.X:f3},{ GXY.Y + relLineEndXY.Y:f3},{ gz1 + relEndZ:f3}\t";
                    if (DispProg.Head_Operation == TaskDisp.EHeadOperation.Sync)
                    {
                        double gz2 = TaskGantry.EncoderPos(TaskGantry.GZ2Axis);
                        str += $"C2,R2={DispProg.RunTime.Head_CR[1].X},{DispProg.RunTime.Head_CR[1].Y}\t";
                        str += $"X2,Y2,Z2={GX2Y2.X:f3},{GX2Y2.Y:f3},{gz2:f3} X2,Y2,Z2={GX2Y2.X + relLineEndXY.X:f3},{GX2Y2.Y + relLineEndXY.Y:f3},{gz2 + relEndZ:f3}\t";
                        double zdiff = TaskDisp.Head_ZSensor_RefPosZ[1] - TaskDisp.Head_ZSensor_RefPosZ[0];
                        str += $"XA2,YA2,ZA2={GXY.X + (absStart[1].X - absStart[0].X):f3},{GXY.Y + (absStart[1].Y - absStart[0].Y):f3},{gz2 - TaskDisp.Head_ZSensor_RefPosZ[1] - TaskDisp.Head_ZSensor_RefPosZ[0]:f3}\t";
                    }
                    GLog.WriteProcessLog(str);
                }
            }
            catch (Exception Ex)
            {
                GDefine.Status = EStatus.ErrorInit;
                TaskDisp.TrigOff(true, true);
                string eMsg = Line.Cmd.ToString() + (char)13 + Ex.Message.ToString();
                throw new Exception(eMsg);
            }
        _End:
            GDefine.Status = EStatus.Ready;
            return true;
        _Stop:
            GDefine.Status = EStatus.Stop;
            return false;
        _Error:
            GDefine.Status = EStatus.ErrorInit;
            return false;
        }
    }

    internal class MeasTemp
    {
        public static bool Execute(DispProg.TLine Line, ERunMode RunMode, double f_origin_x, double f_origin_y, double f_origin_z)
        {
            int points = Line.IPara[1];
            if (points == 0) return true;

            try
            {
                GDefine.Status = EStatus.Busy;

                if (!TaskDisp.TaskMoveGZZ2Up()) return false;

                switch (RunMode)
                {
                    case ERunMode.Dry:
                    case ERunMode.Normal:
                        TaskVision.LightingOff();
                        break;
                    case ERunMode.Camera:
                        TaskVision.LightingOn(TaskVision.DefLightRGB);
                        break;
                }

                if (GDefine.GantryConfig == GDefine.EGantryConfig.XY_ZX2Y2_Z2)
                {
                    TPos2 GX2Y2 = new TPos2(TaskDisp.Head2_DefPos.X, TaskDisp.Head2_DefPos.Y);

                    if (!TaskGantry.SetMotionParamGX2Y2()) goto _Error;
                    if (!TaskGantry.MoveAbsGX2Y2(GX2Y2.X, GX2Y2.Y, false)) goto _Error;

                    TaskGantry.WaitGX2Y2();
                }

                List<TPos2> pos = new List<TPos2>();
                List<double> temp = new List<double>();
                for (int i = 0; i < points; i++)
                {
                    #region assign and translate position
                    double dx = f_origin_x + DispProg.rt_LayoutRelPos[DispProg.RunTime.UIndex].X + Line.X[i];
                    double dy = f_origin_y + DispProg.rt_LayoutRelPos[DispProg.RunTime.UIndex].Y + Line.Y[i];
                    DispProg.TranslatePos(dx, dy, DispProg.rt_Head1RefData, ref dx, ref dy);

                    TPos2 GXY = new TPos2(dx, dy);
                    #endregion

                    #region Move To Pos
                    switch (RunMode)
                    {
                        case ERunMode.Normal:
                        case ERunMode.Dry:
                            {
                                GXY.X = GXY.X + TaskDisp.TempSensor_Ofst.X;
                                GXY.Y = GXY.Y + TaskDisp.TempSensor_Ofst.Y;
                                break;
                            }
                        case ERunMode.Camera:
                        default:
                            {
                                break;
                            }
                    }

                    if (!TaskGantry.SetMotionParamGXY()) goto _Error;
                    if (!TaskGantry.MoveAbsGXY(GXY.X, GXY.Y, false)) goto _Error;
                    TaskGantry.WaitGXY();
                    #endregion

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    //int SettleTime = Line.IPara[4];
                    while (sw.ElapsedMilliseconds < TaskLaser.TempSensor_SettleTime) Thread.Sleep(0);

                    double d = 0;
                    TFTempSensor.GetTemp(ref d);

                    pos.Add(GXY);
                    temp.Add(d);
                }

                if (DispProg.Options_EnableProcessLog)
                {
                    string str = $"{Line.Cmd}\t";
                    str += $"Cal\t";
                    str += $"OX,OY={TaskDisp.TempSensor_Ofst.X:f3},{TaskDisp.TempSensor_Ofst.Y:f3}\t";
                    GLog.WriteProcessLog(str);

                    str = $"{Line.Cmd}\t";
                    str += $"C,R={DispProg.RunTime.Head_CR[0].X},{DispProg.RunTime.Head_CR[0].Y}\t";
                    for (int i = 0; i < temp.Count; i++)
                    {
                        str += $"X,Y,T={pos[i].X},{pos[i].Y},{temp[i]:f1}\t";
                    }
                    GLog.WriteProcessLog(str);
                }
            }
            catch (Exception Ex)
            {
                GDefine.Status = EStatus.ErrorInit;
                throw new Exception(MethodBase.GetCurrentMethod().Name.ToString() + '\r' + Ex.Message.ToString());
            }
            GDefine.Status = EStatus.Ready;
            return true;
        _Error:
            GDefine.Status = EStatus.ErrorInit;
            return false;
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using Emgu.CV;

namespace NDispWin
{
    internal partial class frm_DispCore_DispProg_BdOrient : Form
    {
        public DispProg.TLine CmdLine = new DispProg.TLine();
        public int ProgNo = 0;
        public int LineNo = 0;
        public TPos2 SubOrigin = new TPos2(0, 0);

        public frm_DispCore_DispProg_BdOrient()
        {
            InitializeComponent();
            GControl.LogForm(this);

            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Left = 0;
            Top = 0;
            //TopLevel = false;
            TopMost = true;
            BringToFront();

            lbl_NoImage.Visible = false;

            //AppLanguage.Func.SetComponent(this);
        }

        public void UpdateDisplay()
        {
            lbl_CameraID.Text = CmdLine.IPara[2].ToString();
            lbl_FocusNo.Text = CmdLine.IPara[21].ToString();

            lbl_X1Y1.Text = CmdLine.X[0].ToString("F3") + ", " + CmdLine.Y[0].ToString("F3");

            lbl_XYTol.Text = CmdLine.DPara[1].ToString("F3");
            lbl_MinScore.Text = (CmdLine.DPara[0] * 100).ToString("F0");
            lbl_InvertJudgement.Text = Convert.ToBoolean(CmdLine.IPara[0]).ToString();

            lbl_SettleTime.Text = CmdLine.IPara[4].ToString();
            lbl_FailAction.Text = CmdLine.IPara[6].ToString() + " - " + Enum.GetName(typeof(EFailAction), CmdLine.IPara[6]); 
        }

        private void UpdateImages()
        {
            lbl_NoImage.Visible = false;

            try
            {
                pbox_PatImage.Image = TaskVision.BdOrientTemplate.Image.ToBitmap();
            }
            catch
            {
                lbl_NoImage.Visible = true;
            }
        }

        private string CmdName
        {
            get
            {
                return LineNo.ToString("d3") + " " + CmdLine.Cmd.ToString();
            }
        }

        private void frmDispProg_BdOrient_Load(object sender, EventArgs e)
        {
            GControl.UpdateFormControl(this);
            AppLanguage.Func2.UpdateText(this);

            this.Text = CmdName;

            CmdLine.Copy(DispProg.Script[ProgNo].CmdList.Line[LineNo]);

            TaskVision.SelectedCam = (ECamNo)CmdLine.IPara[2];
            TaskVision.LightingOn(TaskVision.BdOrientLightRGB);

            if (CmdLine.DPara[0] == 0) CmdLine.DPara[0] = 0.85;
            if (CmdLine.DPara[1] == 0) CmdLine.DPara[1] = 2;
            if (CmdLine.IPara[4] == 0) CmdLine.IPara[4] = 150;

            try
            {
                TaskDisp.TaskMoveGZFocus(CmdLine.IPara[21]);
            }
            catch { };

            UpdateDisplay();
            UpdateImages();
        }

        private void btn_SetPt1Pos_Click(object sender, EventArgs e)
        {
            double X = TaskGantry.GXPos();
            double Y = TaskGantry.GYPos();

            NSW.Net.Point2D Old = new NSW.Net.Point2D(CmdLine.X[0], CmdLine.Y[0]);
            CmdLine.X[0] = X - (DispProg.Origin(DispProg.rt_StationNo).X + SubOrigin.X);
            CmdLine.Y[0] = Y - (DispProg.Origin(DispProg.rt_StationNo).Y + SubOrigin.Y);
            NSW.Net.Point2D New = new NSW.Net.Point2D(CmdLine.X[0], CmdLine.Y[0]);
            Log.OnSet(CmdName + ", Position XY", Old, New);

            UpdateDisplay();
        }

        private void btn_GotoPt1Pos_Click(object sender, EventArgs e)
        {
            double X = (DispProg.Origin(DispProg.rt_StationNo).X + SubOrigin.X) + CmdLine.X[0];
            double Y = (DispProg.Origin(DispProg.rt_StationNo).Y + SubOrigin.Y) + CmdLine.Y[0];

            //if (!TaskGantry.SetMotionParamGZZ2()) return;
            //if (!TaskGantry.MoveAbsGZZ2(0)) return;
            if (!TaskDisp.TaskMoveGZZ2Up()) return;

            if (!TaskGantry.SetMotionParamGXY()) return;
            if (!TaskGantry.MoveAbsGXY(X, Y)) return;
        }

        private void lbl_XYTol_Click(object sender, EventArgs e)
        {
            UC.AdjustExec(CmdName + ", XY Tolerance (mm)", ref CmdLine.DPara[1], 0, 5);
            UpdateDisplay();
        }

        private void lbl_MinScore_Click(object sender, EventArgs e)
        {
            int i = (int)(CmdLine.DPara[0] * 100);
            UC.AdjustExec(CmdName + ", MinScore (%)", ref i, 1, 99);
            CmdLine.DPara[0] = (double)i / 100;
            UpdateDisplay();
        }

        private void lbl_InvertJudgement_Click(object sender, EventArgs e)
        {
            if (CmdLine.IPara[0] == 0) 
                CmdLine.IPara[0] = 1;
            else
                CmdLine.IPara[0] = 0;
            UpdateDisplay();
        }

        private void btn_Test_Click(object sender, EventArgs e)
        {
            string EMsg = "BdOrient Test";

            if (!TaskDisp.TaskMoveGZFocus(CmdLine.IPara[21])) return;

            double X = 0;
            double Y = 0;
            double S = 0;
            int t = Environment.TickCount;

            int CamID = CmdLine.IPara[2];
            int Threshold = (int)CmdLine.DPara[5];

            try
            {
                Emgu.CV.Image<Emgu.CV.Structure.Gray, byte> FoundTemplate = null;
                TaskVision.MatchTemplate(CamID, TaskVision.BdOrientTemplate, Threshold, out X, out Y, out S, ref FoundTemplate);
                t = Environment.TickCount - t;
            }
            catch (Exception Ex)
            {
                Msg MsgBox = new Msg();
                MsgBox.Show(Messages.UNKNOWN_EX_ERR, MethodBase.GetCurrentMethod().Name.ToString() + '\r' + Ex.Message.ToString());
            }

            double v_ox = X * TaskVision.DistPerPixelX[CamID];
            double v_oy = Y * TaskVision.DistPerPixelY[CamID];
            double v_s = S;

            bool OK = (Math.Abs(v_ox) <= CmdLine.DPara[1]) && (Math.Abs(v_oy) <= CmdLine.DPara[1]) && (Math.Abs(v_s) >= CmdLine.DPara[0]);
            if (CmdLine.IPara[0] > 0) OK = !OK;

            lbl_OfstXY.Text = v_ox.ToString("F3") + ", " + v_oy.ToString("F3");
            lbl_Score.Text = v_s.ToString("F1");
            if (OK)
            {
                lbl_Result.ForeColor = Color.Green;
                lbl_Result.Text = "OK";
            }
            else
            {
                lbl_Result.ForeColor = Color.Red;
                lbl_Result.Text = "NG";
            }
            lbl_Time.Text = t.ToString();
        }

        private void btn_Learn_Click(object sender, EventArgs e)
        {
            if (!TaskDisp.TaskMoveGZFocus(CmdLine.IPara[21])) return;
 
            CmdLine.IPara[2] = (int)TaskVision.SelectedCam;

            int Threshold = (int)CmdLine.DPara[5];
            TaskVision.TeachTemplate(CmdLine.IPara[2], ref TaskVision.BdOrientTemplate, ref Threshold);
            CmdLine.DPara[5] = (double)Threshold;

            TaskVision.LightRGB[CmdLine.ID] = TaskVision.CurrentLightRGBA;
            UpdateImages();
            UpdateDisplay();
        }

        private void btn_OK_Click(object sender, EventArgs e)
        {
            DispProg.Script[ProgNo].CmdList.Line[LineNo].Copy(CmdLine);
            TaskVision.BdOrientLightRGB = TaskVision.CurrentLightRGBA;

            TaskVision.LightingOn(TaskVision.DefLightRGB);
            //frm_DispProg2.Done = true;
            Log.OnAction("OK", CmdName);
            Close();
        }

        private void btn_Cancel_Click(object sender, EventArgs e)
        {
            TaskVision.LightingOn(TaskVision.DefLightRGB);
            //frm_DispProg2.Done = true;
            Log.OnAction("Cancel", CmdName);
            Close();
        }

        private void lbl_SettleTime_Click(object sender, EventArgs e)
        {
            UC.AdjustExec("BdOrient, Settle Time", ref CmdLine.IPara[4], 0, 500);
            UpdateDisplay();
        }

        private void lbl_FailAction_Click(object sender, EventArgs e)
        {
            EFailAction E = EFailAction.Normal;
            UC.AdjustExec(CmdName + ", Fail Action", ref CmdLine.IPara[6], E);
            UpdateDisplay();
        }

        private void label12_Click(object sender, EventArgs e)
        {

        }

        private void lbl_FocusNo_Click(object sender, EventArgs e)
        {
            UC.AdjustExec(CmdName + ", Focus No", ref CmdLine.IPara[21], 0, DispProg.MAX_FOCUS_POS - 1);
            UpdateDisplay();

            try
            {
                TaskDisp.TaskMoveGZFocus(CmdLine.IPara[21]);
            }
            catch { };
        }

        private void frm_DispCore_DispProg_BdOrient_FormClosed(object sender, FormClosedEventArgs e)
        {
            frm_DispProg2.Done = true;
        }
    }
}

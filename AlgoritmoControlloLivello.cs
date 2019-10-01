using DigitalControl.DataType;
using DigitalControl.FW.Class;
using HalconDotNet;
using System;
using System.Collections;
using System.Threading;

namespace DigitalControl.CMTL.Class
{
    public class AlgoritmoControlloLivello : Algoritmo, IDisposable
    {

        private bool disposed = false;

        private DataType.AlgoritmoControlloLivelloParam parametri = null;
        private DBL.LinguaManager linguaMngr = null;

        public AlgoritmoControlloLivello(DBL.LinguaManager linguaMngr)
        {
            this.linguaMngr = linguaMngr;
        }

        public void SetControlloLivelloParam(DataType.AlgoritmoControlloLivelloParam param)
        {
            this.parametri = param;
        }

        #region centraggio

        public void RegolazioniCentraggio(HImage image, CancellationToken token, out ArrayList iconicList, out ElaborateResult result)
        {
            iconicList = new ArrayList();
            result = new ElaborateResult();
            try
            {
                iconicList.Add(new Utilities.ObjectToDisplay(image));

                if (this.parametri.AbilitaControllo)
                {
                    double center = FindPositionBottle(image, this.parametri, true, ref iconicList);

                    result.Success = center != 0;
                }
                else
                {
                    result.Success = true;
                }
            }
            catch (Exception)
            {
                result.Success = false;
            }
        }

        private double FindPositionBottle(HImage Image, DataType.AlgoritmoControlloLivelloParam param, bool useIconic, ref ArrayList iconicList)
        {
            double columnCenter = 0;

            HMeasure MeasureHandle = null;
            HImage ImgEmfatize = null;

            try
            {
                double Rowstart, Rowend, Colstart, Colend;
                int Width, Height;
                Image.GetImageSize(out Width, out Height);

                Rowstart = 0.0;
                Rowend = Height;
                Colstart = Width / 2;
                Colend = Width / 2;

                if (useIconic)
                    iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(Rowstart, Colstart, Rowend, Colend), "red", 1));

                param.RectCentraggio.Column = Width / 2;

                if (useIconic)
                {
                    // genero i due rettangoli per la misura dell'inclinazione bottiglia
                    // solo per visualizzazione
                    HRegion rectangleCentraggio = new HRegion();
                    rectangleCentraggio.GenRectangle2(param.RectCentraggio.Row, Width / 2, 0, param.RectCentraggio.Length1, param.RectCentraggio.Length2);
                    iconicList.Add(new Utilities.ObjectToDisplay(rectangleCentraggio, "red", 1));
                }

                // Determine all edge pairs that have a negative transition, i.e., edge pairs that enclose dark regions.
                const string Interpolation = "nearest_neighbor";
                HTuple RowEdge_first, ColumnEdge_first, Amplitude_first, Distance_first;
                HTuple RowEdge_last, ColumnEdge_last, Amplitude_last, Distance_last;

                ImgEmfatize = Image.Emphasize(7, 7, param.EmphasizeFactorx10 / 10.0);

                // Misura degli edge sul primo rettangolo
                MeasureHandle = new HMeasure(param.RectCentraggio.Row, Width / 2, 0, param.RectCentraggio.Length1, param.RectCentraggio.Length2, Width, Height, Interpolation);
                MeasureHandle.MeasurePos(ImgEmfatize, param.Sigmax10 / 10.0, param.Thresholdx10 / 10.0, "all", "first", out RowEdge_first, out ColumnEdge_first, out Amplitude_first, out Distance_first);
                MeasureHandle.Dispose();        // Libera lo spazio associato a 'MeasureHandle'

                MeasureHandle = new HMeasure(param.RectCentraggio.Row, Width / 2, Math.PI, param.RectCentraggio.Length1, param.RectCentraggio.Length2, Width, Height, Interpolation);
                MeasureHandle.MeasurePos(ImgEmfatize, param.Sigmax10 / 10.0, param.Thresholdx10 / 10.0, "all", "first", out RowEdge_last, out ColumnEdge_last, out Amplitude_last, out Distance_last);
                MeasureHandle.Dispose();        // Libera lo spazio associato a 'MeasureHandle'

                if (RowEdge_first.Length == 1 && RowEdge_last.Length == 1 && ColumnEdge_first.Length == 1 && ColumnEdge_last.Length == 1)
                {
                    columnCenter = (ColumnEdge_last.D + ColumnEdge_first.D) / 2;

                    if (useIconic)
                    {
                        iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(RowEdge_first.D, ColumnEdge_first.D, 15, Math.PI / 4), "magenta", 1));
                        iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(RowEdge_last.D, ColumnEdge_last.D, 15, Math.PI / 4), "magenta", 1));
                        iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(RowEdge_last.D, columnCenter, 15, Math.PI / 4), "green", 1));
                    }
                }
                else
                {
                    columnCenter = Width / 2;
                }
            }
            catch (Exception)
            {
                columnCenter = 0;
            }
            finally
            {
                if (MeasureHandle != null) MeasureHandle.Dispose();
                if (ImgEmfatize != null) ImgEmfatize.Dispose();
            }

            return columnCenter;
        }

        #endregion centraggio

        #region livello

        public void RegolazioniLivello(HImage image, CancellationToken token, out ArrayList iconicList, out ElaborateResult result)
        {
            iconicList = new ArrayList();
            result = new ElaborateResult();
            try
            {
                iconicList.Add(new Utilities.ObjectToDisplay(image));

                if (this.parametri.AbilitaControllo)
                {
                    double center = FindPositionBottle(image, this.parametri, false, ref iconicList);

                    bool errorLevelMin, errorLevelMax, errorEmpty;

                    result.Success = AnalisiLivello(image, this.parametri, center, true, out errorLevelMin, out errorLevelMax, out errorEmpty, ref iconicList);
                }
                else
                {
                    result.Success = true;
                }
            }
            catch (Exception)
            {
                result.Success = false;
            }
        }

        private bool AnalisiLivello(HImage Img, DataType.AlgoritmoControlloLivelloParam param, double centerColumn, bool regolazioni, out bool errorLevelMin, out bool errorLevelMax, out bool errorEmpty, ref ArrayList iconicList)
        {
            bool ret = false;


            errorLevelMin = false;
            errorLevelMax = false;
            errorEmpty = false;


            int Width, Height;
            Img.GetImageSize(out Width, out Height);

            const string Interpolation = "nearest_neighbor";
            string Transition = param.LiquidoChiaro ? "negative" : "positive";
            const string Select = "first";
            const double delta = 30.0;

            // Disegna la regione per il calcolo del Livello
            HRegion Rectangle = new HRegion();
            Rectangle.GenRectangle2(param.RectLivello.Row, centerColumn, param.RectLivello.Angle, param.RectLivello.Length1, param.RectLivello.Length2);

            HMeasure MeasureHandle = new HMeasure(param.RectLivello.Row, centerColumn, param.RectLivello.Angle, param.RectLivello.Length1, param.RectLivello.Length2, Width, Height, Interpolation);

            HTuple RowEdge, ColumnEdge, Amplitude, Distance;
            Img.MeasurePos(MeasureHandle, param.SigmaLivellox10 / 10.0, param.ThresholdLivellox10 / 10.0, Transition, Select, out RowEdge, out ColumnEdge, out Amplitude, out Distance);

            MeasureHandle.Dispose();

            if (RowEdge.Length > 0)
            {
                double altezzaSchiuma = RowEdge.D;

                if (altezzaSchiuma >= param.RowMaxLivello && altezzaSchiuma <= param.RowMinLivello)
                {
                    if (param.LiquidoChiaro)
                    {
                        ret = true;
                    }
                    else
                    {
                        HRegion rectangleThreshold = new HRegion();
                        //double row = altezzaSchiuma + (paramLevel.rowMin - altezzaSchiuma) / 2;
                        double row = altezzaSchiuma + (param.RectLivello.Row + param.RectLivello.Length1 - altezzaSchiuma) / 2;
                        double column = centerColumn;
                        //double length1 = (paramLevel.rowMin - altezzaSchiuma) / 2;
                        double length1 = (param.RectLivello.Row + param.RectLivello.Length1 - altezzaSchiuma) / 2;
                        double length2 = param.RectLivello.Length2;

                        rectangleThreshold.GenRectangle2(row, column, param.RectLivello.Angle, length1, length2);

                        HImage imgReduced = Img.ReduceDomain(rectangleThreshold);
                        HRegion regionThreshold = imgReduced.Threshold((double)param.ThresholdMin, (double)param.ThresholdMax);
                        imgReduced.Dispose();

                        double r, c;
                        int area = rectangleThreshold.AreaCenter(out r, out c);
                        rectangleThreshold.Dispose();

                        HRegion regionSelect = regionThreshold.SelectShape("area", "and", area * 0.7, double.MaxValue);
                        regionThreshold.Dispose();

                        ret = regionSelect.CountObj() == 1;
                        iconicList.Add(new Utilities.ObjectToDisplay(regionSelect, "blue", 3) { DrawMode = "fill" });
                    }
                }
                else if (altezzaSchiuma < param.RowMaxLivello)
                {
                    errorLevelMax = true;
                }
                else if (altezzaSchiuma > param.RowMinLivello)
                {
                    errorLevelMin = true;
                }

                iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(altezzaSchiuma, centerColumn - delta, altezzaSchiuma, centerColumn + delta), ret ? "green" : "red", 3));
            }
            else
            {
                if (param.UseThreshold)
                {
                    if (param.LiquidoChiaro)
                    {
                        HRegion rectangleThreshold = new HRegion();
                        rectangleThreshold.GenRectangle2(param.QuotaControlloVuoto, centerColumn, 0.0, param.LarghezzaControlloVuoto, 20);

                        HImage imgReduced = Img.ReduceDomain(rectangleThreshold);
                        rectangleThreshold.Dispose();

                        HRegion regionThreshold = imgReduced.Threshold((double)param.ThresholdMin, (double)param.ThresholdMax);
                        imgReduced.Dispose();

                        HRegion connectedRegions = regionThreshold.Connection();
                        regionThreshold.Dispose();

                        HRegion filledCandidates = connectedRegions.FillUp();
                        connectedRegions.Dispose();

                        HRegion regionMax = filledCandidates.SelectShapeStd("max_area", 0);

                        double row, col;
                        int area = regionMax.AreaCenter(out row, out col);
                        regionMax.Dispose();

                        if (area > param.SogliaAreaControlloVuoto)
                        {
                            iconicList.Add(new Utilities.ObjectToDisplay(filledCandidates, "green", 1) { DrawMode = "fill" });
                            ret = true;
                        }
                        else
                        {
                            iconicList.Add(new Utilities.ObjectToDisplay(filledCandidates, "red", 1) { DrawMode = "fill" });
                            ret = false;
                        }

                        iconicList.Add(new Utilities.ObjectToDisplay(area.ToString(), "blue", (int)(row - 50), (int)(col - param.LarghezzaControlloVuoto)));
                    }
                    else
                    {
                        HRegion rectangleThreshold = new HRegion();
                        rectangleThreshold.GenRectangle2(param.RowMaxLivello + (param.RowMinLivello - param.RowMaxLivello) / 2
                            , centerColumn
                            , param.RectLivello.Angle
                            , (param.RowMinLivello - param.RowMaxLivello) / 2
                            , param.RectLivello.Length2);

                        HImage imgReduced = Img.ReduceDomain(rectangleThreshold);
                        HRegion regionThreshold = imgReduced.Threshold((double)param.ThresholdMin, (double)param.ThresholdMax);
                        imgReduced.Dispose();

                        double r, c;
                        int area = rectangleThreshold.AreaCenter(out r, out c);

                        HRegion regionSelect = regionThreshold.SelectShape("area", "and", area * 0.7, double.MaxValue);

                        ret = regionSelect.CountObj() == 1;
                        regionSelect.Dispose();

                        iconicList.Add(new Utilities.ObjectToDisplay(rectangleThreshold, "red", 1));
                        iconicList.Add(new Utilities.ObjectToDisplay(regionThreshold, "red", 1) { DrawMode = "fill" });
                    }

                    if (ret == false)
                    {
                        errorEmpty = true;
                    }
                }

            }

            iconicList.Add(new Utilities.ObjectToDisplay(Rectangle, "red", 2));

            iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(param.RowMaxLivello, centerColumn - delta, param.RowMaxLivello, centerColumn + delta), "cyan", 3));
            iconicList.Add(new Utilities.ObjectToDisplay("Max", "cyan", (int)(param.RowMaxLivello - delta), (int)(centerColumn + 2 * delta)));

            iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(param.RowMinLivello, centerColumn - delta, param.RowMinLivello, centerColumn + delta), "cyan", 3));
            iconicList.Add(new Utilities.ObjectToDisplay("Min", "cyan", (int)(param.RowMinLivello), (int)(centerColumn + 2 * delta)));

            return ret;
        }

        #endregion livello

        public void WorkingControlloLivelloAlgorithm(HImage image, CancellationToken token, out ArrayList iconicList, out ElaborateResult result)
        {
            iconicList = new ArrayList();
            result = new ElaborateResult();
            try
            {
                iconicList.Add(new Utilities.ObjectToDisplay(image));

                if (this.parametri == null)
                {
                    iconicList.Add(new Utilities.ObjectToDisplay(linguaMngr.GetTranslation("MSG_NO_RECIPE"), "red", 0, 0));
                    result.Success = true;
                }
                else
                {
                    if (this.parametri.AbilitaControllo)
                    {
                        double center = FindPositionBottle(image, this.parametri, false, ref iconicList);

                        token.ThrowIfCancellationRequested();

                        bool errorLevelMin, errorLevelMax, errorEmpty;

                        result.Success = AnalisiLivello(image, this.parametri, center, true, out errorLevelMin, out errorLevelMax, out errorEmpty, ref iconicList);

                        result.DettaglioElaborazione.Add("KO_LEVEL_MIN", errorLevelMin);
                        result.DettaglioElaborazione.Add("KO_LEVEL_MAX", errorLevelMax);
                        result.DettaglioElaborazione.Add("KO_LEVEL_EMPTY", errorEmpty);

                        token.ThrowIfCancellationRequested();
                    }
                    else
                    {
                        iconicList.Add(new Utilities.ObjectToDisplay(linguaMngr.GetTranslation("MSG_TEST_DISABLED"), "red", 0, 0));
                        result.Success = true;
                    }
                }
            }
            catch (Exception)
            {
                result.Success = false;
            }
        }

        #region IDisposable

        //Implement IDisposable.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects).                           
                }
                // Free your own state (unmanaged objects).
                // Set large fields to null.
                disposed = true;
            }
        }

        // Use C# destructor syntax for finalization code.
        ~AlgoritmoControlloLivello()
        {
            // Simply call Dispose(false).
            Dispose(false);
        }

        #endregion IDisposable

    }
}
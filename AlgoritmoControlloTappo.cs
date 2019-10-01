using DigitalControl.DataType;
using DigitalControl.FW.Class;
using HalconDotNet;
using System;
using System.Collections;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalControl.CMTL.Class
{
    public class AlgoritmoControlloTappo : Algoritmo, IDisposable
    {

        const double delta = 30.0;

        private bool disposed = false;

        private DataType.AlgoritmoControlloTappoParam parametri = null;
        private DBL.LinguaManager linguaMngr = null;

        public AlgoritmoControlloTappo(DBL.LinguaManager linguaMngr)
        {
            this.linguaMngr = linguaMngr;
        }

        public void SetControlloTappoParam(DataType.AlgoritmoControlloTappoParam param)
        {
            this.parametri = param;
        }

        #region centraggio

        public void RegolazioniCentraggioTappo(HImage[] images, CancellationToken token, out ArrayList[] iconicList, out ElaborateResult[] result)
        {
            iconicList = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            result = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            ArrayList[] iconicListArr = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            ElaborateResult[] resultArr = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            Parallel.For(0, Properties.Settings.Default.NumeroCamereTappo, i =>
            {
                ArrayList iconicTmp = new ArrayList();
                iconicListArr[i] = iconicTmp;
                resultArr[i] = new ElaborateResult();

                try
                {

                    iconicTmp.Add(new Utilities.ObjectToDisplay(images[i]));

                    double centerO = 0;
                    double centerV = 0;

                    FindPositionBottle(images[i], this.parametri, true, out centerO, out centerV, ref iconicTmp);

                    resultArr[i].Success = centerO != 0 && centerV != 0;
                }
                catch (Exception ex)
                {
                    ExceptionManager.AddException(ex);
                    resultArr[i].Success = false;
                }
            });

            for (int i = 0; i < Properties.Settings.Default.NumeroCamereTappo; i++)
            {
                iconicList[i] = iconicListArr[i];
                result[i] = resultArr[i];
            }
        }

        private void FindPositionBottle(HImage Image, DataType.AlgoritmoControlloTappoParam param, bool useIconic, out double centerO, out double centerV, ref ArrayList iconicList)
        {
            centerO = 0;
            centerV = 0;

            if (!param.UsaCentraggioPET)
            {
                centerO = FindPositionBottleOrizzontaleBase(Image, param.Centraggio, useIconic, ref iconicList);
                centerV = FindPositionBottleVerticaleBase(Image, param.Centraggio, centerO, useIconic, ref iconicList);
            }

            if (param.UsaCentraggioPET)
                FindPositionBottlePET(Image, param.CentraggioPET, useIconic, out centerO, out centerV, ref iconicList);

        }


        private double FindPositionBottleOrizzontaleBase(HImage Image, DataType.CentraggioParam param, bool useIconic, ref ArrayList iconicList)
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

                if (useIconic)
                {
                    HRegion rectangleCentraggio = new HRegion();
                    rectangleCentraggio.GenRectangle2(param.RectCentraggioOrizzontale.Row, Width / 2, 0, param.RectCentraggioOrizzontale.Length1, param.RectCentraggioOrizzontale.Length2);
                    iconicList.Add(new Utilities.ObjectToDisplay(rectangleCentraggio, "red", 1));
                }

                // Determine all edge pairs that have a negative transition, i.e., edge pairs that enclose dark regions.

                HTuple RowEdge_first, ColumnEdge_first, Amplitude_first, Distance_first;
                HTuple RowEdge_last, ColumnEdge_last, Amplitude_last, Distance_last;

                ImgEmfatize = Image.Emphasize(7, 7, param.EmphasizeFactor);

                // Misura degli edge sul primo rettangolo
                MeasureHandle = new HMeasure(param.RectCentraggioOrizzontale.Row, Width / 2, 0, param.RectCentraggioOrizzontale.Length1, param.RectCentraggioOrizzontale.Length2, Width, Height, "nearest_neighbor");
                MeasureHandle.MeasurePos(ImgEmfatize, param.Sigma, param.Threshold, "all", "first", out RowEdge_first, out ColumnEdge_first, out Amplitude_first, out Distance_first);
                MeasureHandle.Dispose();        // Libera lo spazio associato a 'MeasureHandle'

                MeasureHandle = new HMeasure(param.RectCentraggioOrizzontale.Row, Width / 2, Math.PI, param.RectCentraggioOrizzontale.Length1, param.RectCentraggioOrizzontale.Length2, Width, Height, "nearest_neighbor");
                MeasureHandle.MeasurePos(ImgEmfatize, param.Sigma, param.Threshold, "all", "first", out RowEdge_last, out ColumnEdge_last, out Amplitude_last, out Distance_last);
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

        private double FindPositionBottleVerticaleBase(HImage Img, DataType.CentraggioParam param, double centerColumn, bool useIconic, ref ArrayList iconicList)
        {
            double ret = 0;

            int Width, Height;
            Img.GetImageSize(out Width, out Height);

            HRegion rectangleCentraggio = new HRegion();
            rectangleCentraggio.GenRectangle2(param.RectCentraggioVerticale.Row, centerColumn, param.RectCentraggioVerticale.Angle, param.RectCentraggioVerticale.Length1, param.RectCentraggioVerticale.Length2);

            if (useIconic)
                iconicList.Add(new Utilities.ObjectToDisplay(rectangleCentraggio, "red", 1));

            HTuple RowEdge1, ColumnEdge1, Amplitude1, Distance1;
            HMeasure MeasureHandle = new HMeasure(param.RectCentraggioVerticale.Row, centerColumn, param.RectCentraggioVerticale.Angle, param.RectCentraggioVerticale.Length1, param.RectCentraggioVerticale.Length2, Width, Height, "nearest_neighbor");
            Img.MeasurePos(MeasureHandle, param.Sigma, param.Threshold, "all", "last", out RowEdge1, out ColumnEdge1, out Amplitude1, out Distance1);
            MeasureHandle.Dispose();

            if (RowEdge1.Length > 0)
            {
                ret = RowEdge1.D;
                if (useIconic)
                    iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(ret, centerColumn - delta, ret, centerColumn + delta), "cyan", 3));
            }

            return ret;
        }

        private void FindPositionBottlePET(HImage Image, DataType.CentraggioPETParam param, bool useIconic, out double centerO, out double centerV, ref ArrayList iconicList)
        {
            try
            {
                centerO = 0;
                centerV = 0;

                if (useIconic)
                {
                    HRegion rectangleCentraggio = new HRegion();
                    rectangleCentraggio.GenRectangle2(param.RectCentraggioOrizzontale.Row, param.RectCentraggioOrizzontale.Column, 0, param.RectCentraggioOrizzontale.Length1, param.RectCentraggioOrizzontale.Length2);
                    iconicList.Add(new Utilities.ObjectToDisplay(rectangleCentraggio, "red", 1));
                }

                HImage imgReduced = Image.ReduceDomain(GetRegion(param.RectCentraggioOrizzontale));
                HRegion region = imgReduced.Threshold(0.0, (double)param.Threshold);
                imgReduced.Dispose();

                HRegion regionErosion = region.ErosionRectangle1(param.KernelPreElab, 1);
                region.Dispose();
                HRegion regionDilation = regionErosion.DilationRectangle1(param.KernelPreElab, 1);
                regionErosion.Dispose();

                //HRegion regionClosing = region.ClosingRectangle1(10, 10);
                //region.Dispose();
                //HRegion regionErosion = regionClosing.ErosionRectangle1(param.KernelPreElab, 1);
                //regionClosing.Dispose();
                //HRegion regionDilation = regionErosion.DilationRectangle1(param.KernelPreElab, 1);
                //regionErosion.Dispose();

                HTuple row;
                HTuple columnBegin;
                HTuple columnEnd;

                GetRegionRuns(regionDilation, out row, out columnBegin, out columnEnd);

                if (useIconic)
                {
                    iconicList.Add(new Utilities.ObjectToDisplay(regionDilation, "blue", 2));
                }
                else
                {
                    regionDilation.Dispose();
                }

                int numberLines = row.TupleLength();

                int numMinCampioni = 5;

                if (numberLines > numMinCampioni)
                {
                    row = row.TupleSelectRange(numMinCampioni, numberLines - numMinCampioni);
                    columnBegin = columnBegin.TupleSelectRange(numMinCampioni, numberLines - numMinCampioni);
                    columnEnd = columnEnd.TupleSelectRange(numMinCampioni, numberLines - numMinCampioni);

                    HTuple diameter = columnEnd - columnBegin;

                    HTuple minDiameter = diameter.TupleMin();
                    HTuple minDiameterFound = minDiameter;

                    int indiceColloMin = -1;

                    int cntConsecutivi = 0;

                    numberLines = row.TupleLength();

                    for (int i = numberLines - 1; i > 0; i--)
                    {
                        HTuple val = columnEnd.TupleSelect(i) - columnBegin.TupleSelect(i);

                        if ((minDiameter - val).TupleFabs() < param.DeltaMinimo)
                        {
                            cntConsecutivi++;
                        }
                        else
                        {
                            cntConsecutivi = 0;
                        }

                        if (cntConsecutivi > 30)
                        {
                            if (val < minDiameterFound || indiceColloMin == -1)
                                minDiameterFound = val;

                            indiceColloMin = i;

                            centerO = (columnBegin[indiceColloMin].D + columnEnd[indiceColloMin].D) / 2.0;

                            break;
                        }

                    }

                    if (useIconic)
                    {
                        //iconicList.Add(new Utilities.ObjectToDisplay(string.Format("Diameter = {0}", minDiameter.D), "red", 0, 0));
                        //iconicList.Add(new Utilities.ObjectToDisplay(string.Format("Diameter found = {0}", minDiameterFound.D), "red", 30, 0));

                        iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(row[indiceColloMin].D, columnBegin[indiceColloMin].D, 15, Math.PI / 4), "magenta", 1));
                        iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(row[indiceColloMin].D, columnEnd[indiceColloMin].D, 15, Math.PI / 4), "magenta", 1));
                        iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(row[indiceColloMin].D, centerO, 15, Math.PI / 4), "green", 1));
                    }

                    int indiceColloMax = -1;

                    for (int i = indiceColloMin; i > 0; i--)
                    {
                        HTuple val = columnEnd.TupleSelect(i) - columnBegin.TupleSelect(i);

                        if ((minDiameterFound - val).TupleFabs() > 30)
                        {
                            indiceColloMax = i;

                            centerV = row.TupleSelect(i);

                            break;
                        }
                    }

                    if (useIconic)
                    {
                        iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(row[indiceColloMax].D, columnBegin[indiceColloMax].D, 15, Math.PI / 4), "red", 1));
                        iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(row[indiceColloMax].D, columnEnd[indiceColloMax].D, 15, Math.PI / 4), "red", 1));

                        iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(centerV, centerO, 15, Math.PI / 4), "yellow", 1));
                    }

                }

            }
            catch (Exception)
            {
                centerO = 0;
                centerV = 0;
            }
        }


        private void GetRegionRuns(HRegion region, out HTuple row, out HTuple columnBegin, out HTuple columnEnd)
        {
            HTuple rowTmp;
            HTuple columnBeginTmp;
            HTuple columnEndTmp;

            region.GetRegionRuns(out rowTmp, out columnBeginTmp, out columnEndTmp);

            row = new HTuple();
            columnBegin = new HTuple();
            columnEnd = new HTuple();

            int cnt = rowTmp.TupleLength();

            if (cnt > 0)
            {
                int rowStart = rowTmp.TupleSelect(0);
                long prevRow = -1;

                for (int i = 0; i < cnt; i++)
                {
                    if (prevRow == rowTmp.TupleSelect(i).L)
                    {
                        int cntTmp = row.TupleLength();

                        if (columnBegin.TupleSelect(cntTmp - 1) > columnBeginTmp.TupleSelect(i))
                        {
                            columnBegin[cntTmp - 1] = columnBeginTmp.TupleSelect(i);
                        }

                        if (columnEnd.TupleSelect(cntTmp - 1) < columnEndTmp.TupleSelect(i))
                        {
                            columnEnd[cntTmp - 1] = columnEndTmp.TupleSelect(i);
                        }
                    }
                    else
                    {
                        row = row.TupleConcat(rowTmp.TupleSelect(i));
                        columnBegin = columnBegin.TupleConcat(columnBeginTmp.TupleSelect(i));
                        columnEnd = columnEnd.TupleConcat(columnEndTmp.TupleSelect(i));
                    }

                    prevRow = rowTmp.TupleSelect(i).L;
                }
            }
        }

        #endregion centraggio

        #region presenza tappo

        public void RegolazioniPresenzaTappo(HImage[] images, CancellationToken token, out ArrayList[] iconicList, out ElaborateResult[] result)
        {
            iconicList = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            result = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            ArrayList[] iconicListArr = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            ElaborateResult[] resultArr = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            Parallel.For(0, Properties.Settings.Default.NumeroCamereTappo, i =>
            {
                ArrayList iconicTmp = new ArrayList();
                iconicListArr[i] = iconicTmp;
                resultArr[i] = new ElaborateResult();

                try
                {
                    iconicTmp.Add(new Utilities.ObjectToDisplay(images[i]));

                    if (this.parametri.AbilitaPresenza)
                    {
                        double centerO = 0;
                        double centerV = 0;

                        FindPositionBottle(images[i], this.parametri, false, out centerO, out centerV, ref iconicTmp);

                        bool ok = AnalisiPresenzaTappo(images[i], this.parametri.Presenza, centerO, centerV, true, ref iconicTmp);

                        resultArr[i].Success = ok;
                    }
                    else
                    {
                        iconicTmp.Add(new Utilities.ObjectToDisplay(linguaMngr.GetTranslation("MSG_TEST_DISABLED"), "red", 0, 0));

                        resultArr[i].Success = true;
                    }
                }
                catch (Exception ex)
                {
                    ExceptionManager.AddException(ex);
                    resultArr[i].Success = false;
                }

            });

            for (int i = 0; i < Properties.Settings.Default.NumeroCamereTappo; i++)
            {
                iconicList[i] = iconicListArr[i];
                result[i] = resultArr[i];
            }

        }

        private bool AnalisiPresenzaTappo(HImage Img, DataType.PresenzaParam param, double centerColumn, double centerRow, bool regolazioni, ref ArrayList iconicList)
        {
            bool ret = false;

            // Disegna la regione per il calcolo dell'istogramma dei colori
            HRegion rectangleTappo = new HRegion();

            rectangleTappo.GenRectangle2(centerRow + param.DeltaYRectDaTappo + param.DimensioneRect, centerColumn, 0, param.DimensioneRect, param.DimensioneRect);

            HTuple absoluteHisto, relativeHisto;
            absoluteHisto = Img.GrayHisto(rectangleTappo, out relativeHisto);

            int totalPixel = absoluteHisto.TupleSum();

            int indexStart = param.MinValue;
            int indexStop = param.MaxValue;

            long sommaPixel = 0;

            if ((indexStart < absoluteHisto.LArr.Length) && (indexStop < absoluteHisto.LArr.Length))
            {
                for (int i = indexStart; i <= indexStop; i++)
                {
                    sommaPixel += absoluteHisto.LArr[i];
                }
            }

            if (sommaPixel >= param.Soglia)
            {
                // OK il tappo è presente
                ret = true;
            }
            else
            {
                // Error il tappo è assente
                ret = false;
            }

            iconicList.Add(new Utilities.ObjectToDisplay(rectangleTappo, "orange", 2));

            int textRow = Convert.ToInt16(centerRow + param.DeltaYRectDaTappo + param.DimensioneRect - 16);
            int textColumn = Convert.ToInt16(centerColumn - 8);

            iconicList.Add(new Utilities.ObjectToDisplay("V", ret ? "green" : "red", textRow, textColumn));

            if (regolazioni)
            {
                // In regolazioni visualizzo l'histogramma colore
                HFunction1D function = new HFunction1D(absoluteHisto);
                HFunction1D smoothedFunction = function.SmoothFunct1dGauss(2.0);
                HTuple xValues, yValues;
                smoothedFunction.Funct1dToPairs(out xValues, out yValues);

                HRegion histoRegion2 = new HRegion();

                const int TableRow = 256;
                const int TableColumn = 256;

                histoRegion2.GenRegionHisto(yValues, TableRow, TableColumn, 1);

                iconicList.Add(new Utilities.ObjectToDisplay(histoRegion2, "green", 1));

                textRow = Convert.ToInt16(TableRow + TableColumn * 0.5);
                int textColumn1 = 128 + indexStart;
                int textColumn2 = 128 + indexStop;

                iconicList.Add(new Utilities.ObjectToDisplay("|", "red", textRow - 50, textColumn1));
                iconicList.Add(new Utilities.ObjectToDisplay("|", "red", textRow - 50, textColumn2));
                iconicList.Add(new Utilities.ObjectToDisplay(sommaPixel.ToString(), "red", TableRow, textColumn1));

            }

            return ret;
        }

        #endregion presenza tappo

        #region serraggio

        public void RegolazioniSerraggioTappo(HImage[] images, CancellationToken token, out ArrayList[] iconicList, out ElaborateResult[] result)
        {
            iconicList = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            result = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            ArrayList[] iconicListArr = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            ElaborateResult[] resultArr = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            Parallel.For(0, Properties.Settings.Default.NumeroCamereTappo, i =>
            {
                ArrayList iconicTmp = new ArrayList();
                iconicListArr[i] = iconicTmp;
                resultArr[i] = new ElaborateResult();

                try
                {

                    iconicTmp.Add(new Utilities.ObjectToDisplay(images[i]));

                    if (this.parametri.AbilitaSerraggio)
                    {
                        double centerO = 0;
                        double centerV = 0;

                        FindPositionBottle(images[i], this.parametri, false, out centerO, out centerV, ref iconicTmp);

                        bool ok = AnalisiSerraggioTappo(images[i], this.parametri.Serraggio, centerO, centerV, ref iconicTmp);

                        resultArr[i].Success = ok;
                    }
                    else
                    {
                        iconicTmp.Add(new Utilities.ObjectToDisplay(linguaMngr.GetTranslation("MSG_TEST_DISABLED"), "red", 0, 0));

                        resultArr[i].Success = true;
                    }
                }
                catch (Exception ex)
                {
                    ExceptionManager.AddException(ex);
                    resultArr[i].Success = false;
                }
            });

            for (int i = 0; i < Properties.Settings.Default.NumeroCamereTappo; i++)
            {
                iconicList[i] = iconicListArr[i];
                result[i] = resultArr[i];
            }

        }

        private bool AnalisiSerraggioTappo(HImage Img, DataType.SerraggioParam param, double centerColumn, double centerRow, ref ArrayList iconicList)
        {

            bool ret = false;

            HRegion rectangleSerraggio = new HRegion();

            rectangleSerraggio.GenRectangle2(centerRow + param.DeltaYRectDaTappo + param.AltezzaRect, centerColumn, 0, param.LarghezzaRect, param.AltezzaRect);

            HImage imageReduced = Img.ReduceDomain(rectangleSerraggio);
            HRegion regionThr = imageReduced.Threshold((double)param.ThresholdMin, (double)param.ThresholdMax);
            imageReduced.Dispose();
            HRegion connectedRegions = regionThr.Connection();
            regionThr.Dispose();
            HRegion unionRegion = connectedRegions.Union1();
            connectedRegions.Dispose();
            double row, column, phi, length1, length2;
            unionRegion.SmallestRectangle2(out row, out column, out phi, out length1, out length2);
            HRegion smallRectRegion = new HRegion();
            smallRectRegion.GenRectangle2(row, column, phi, length1, length2);

            if (length2 < param.Soglia)
            {
                ret = true;
            }

            iconicList.Add(new Utilities.ObjectToDisplay(rectangleSerraggio, "green", 2));
            iconicList.Add(new Utilities.ObjectToDisplay(unionRegion, ret ? "green" : "red", 2) { DrawMode = "fill" });
            iconicList.Add(new Utilities.ObjectToDisplay(smallRectRegion, "cyan", 2));

            int TextRow = Convert.ToInt16(centerRow + param.DeltaYRectDaTappo + param.AltezzaRect - 16);
            int TextColumn = Convert.ToInt16(centerColumn - 8);

            iconicList.Add(new Utilities.ObjectToDisplay("√", ret ? "green" : "red", TextRow, TextColumn));

            return ret;
        }

        #endregion serraggio


        //================================================================================================================================================
        #region Algoritmo Livello

        public void RegolazioniLivello(HImage[] images, CancellationToken token, out ArrayList[] iconicLists, out ElaborateResult[] results)
        {
            ArrayList iconicList = new ArrayList();
            ElaborateResult result = new ElaborateResult();
            try
            {
                iconicList.Add(new Utilities.ObjectToDisplay(images[0]));

                if (this.parametri.AbilitaControlloLivello)
                {
                    double centerO = 0;
                    double centerV = 0;

                    FindPositionBottle(images[0], this.parametri, false, out centerO, out centerV, ref iconicList);

                    bool errorLevelMin, errorLevelMax, errorEmpty;

                    result.Success = AnalisiLivello(images[0], this.parametri.Livello, centerO, true, out errorLevelMin, out errorLevelMax, out errorEmpty, ref iconicList);
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
            finally
            {
                iconicLists = new ArrayList[] { iconicList };
                results = new ElaborateResult[] { result };
            }
        }

        private bool AnalisiLivello(HImage Img, DataType.LivelloParam param, double centerColumn, bool regolazioni, out bool errorLevelMin, out bool errorLevelMax, out bool errorEmpty, ref ArrayList iconicList)
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

            double minLivello = Height - param.RowMinLivello;
            double maxLivello = Height - param.RowMaxLivello;
            double minLivelloRect = minLivello + 50;
            double maxLivelloRect = maxLivello - 50;

            double row = maxLivelloRect + (minLivelloRect - maxLivelloRect) / 2.0;
            double column = centerColumn;
            double phi = Math.PI / 2;
            double length1 = (minLivelloRect - maxLivelloRect) / 2.0;
            double length2 = 15;


            // Disegna la regione per il calcolo del Livello
            HRegion Rectangle = new HRegion();
            Rectangle.GenRectangle2(row, column, phi, length1, length2);

            HMeasure MeasureHandle = new HMeasure(row, column, phi, length1, length2, Width, Height, Interpolation);

            HTuple RowEdge, ColumnEdge, Amplitude, Distance;
            Img.MeasurePos(MeasureHandle, param.SigmaLivellox10 / 10.0, param.ThresholdLivellox10 / 10.0, Transition, Select, out RowEdge, out ColumnEdge, out Amplitude, out Distance);

            MeasureHandle.Dispose();

            if (RowEdge.Length > 0)
            {
                double altezzaSchiuma = RowEdge.D;

                if (altezzaSchiuma <= minLivello && altezzaSchiuma >= maxLivello)
                {
                    if (param.LiquidoChiaro)
                    {
                        ret = true;
                    }
                    else
                    {
                        HRegion rectangleThreshold = new HRegion();
                        double rowThr = altezzaSchiuma + (row + length1 - altezzaSchiuma) / 2;
                        double columnThr = centerColumn;
                        double length1Thr = (row + length1 - altezzaSchiuma) / 2;
                        double length2Thr = length2;

                        rectangleThreshold.GenRectangle2(rowThr, columnThr, phi, length1Thr, length2Thr);

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

                        double rowCv, colCv;
                        int area = regionMax.AreaCenter(out rowCv, out colCv);
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

                        iconicList.Add(new Utilities.ObjectToDisplay(area.ToString(), "blue", (int)(row - 50), (int)(colCv - param.LarghezzaControlloVuoto)));
                    }
                    else
                    {
                        HRegion rectangleThreshold = new HRegion();
                        double rowThr = row;
                        double columnThr = centerColumn;
                        double length1Thr = length1;
                        double length2Thr = length2;

                        rectangleThreshold.GenRectangle2(rowThr, columnThr, phi, length1Thr, length2Thr);

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

            iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(maxLivello, centerColumn - delta, maxLivello, centerColumn + delta), "cyan", 3));
            iconicList.Add(new Utilities.ObjectToDisplay("Max", "cyan", (int)(maxLivello - delta), (int)(centerColumn + 2 * delta)));

            iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(minLivello, centerColumn - delta, minLivello, centerColumn + delta), "cyan", 3));
            iconicList.Add(new Utilities.ObjectToDisplay("Min", "cyan", (int)(minLivello), (int)(centerColumn + 2 * delta)));

            return ret;
        }





        private bool AnalisiLivello_old(HImage Img, DataType.LivelloParam param, double centerColumn, bool regolazioni, out bool errorLevelMin, out bool errorLevelMax, out bool errorEmpty, ref ArrayList iconicList)
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

        #endregion Algoritmo Livello
        //****************************************---------------------------------------------*************************************************************



        #region serraggio stelvin

        public void RegolazioniSerraggioStelvinTappo(HImage[] images, CancellationToken token, out ArrayList[] iconicList, out ElaborateResult[] result)
        {
            iconicList = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            result = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            ArrayList[] iconicListArr = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            ElaborateResult[] resultArr = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            Parallel.For(0, Properties.Settings.Default.NumeroCamereTappo, i =>
            {

                ArrayList iconicTmp = new ArrayList();
                iconicListArr[i] = iconicTmp;
                resultArr[i] = new ElaborateResult();

                try
                {

                    iconicTmp.Add(new Utilities.ObjectToDisplay(images[i]));

                    if (this.parametri.AbilitaSerraggioStelvin)
                    {
                        double centerO = 0;
                        double centerV = 0;

                        FindPositionBottle(images[i], this.parametri, false, out centerO, out centerV, ref iconicTmp);

                        bool ok = AnalisiSerraggioStelvinTappo(images[i], this.parametri.SerraggioStelvin, centerO, centerV, ref iconicTmp);

                        resultArr[i].Success = ok;
                    }
                    else
                    {
                        iconicTmp.Add(new Utilities.ObjectToDisplay(linguaMngr.GetTranslation("MSG_TEST_DISABLED"), "red", 0, 0));

                        resultArr[i].Success = true;
                    }

                }
                catch (Exception ex)
                {
                    ExceptionManager.AddException(ex);
                    resultArr[i].Success = false;
                }
            });

            for (int i = 0; i < Properties.Settings.Default.NumeroCamereTappo; i++)
            {
                iconicList[i] = iconicListArr[i];
                result[i] = resultArr[i];
            }

        }

        private bool AnalisiSerraggioStelvinTappo(HImage Img, DataType.SerraggioStelvinParam param, double centerColumn, double centerRow, ref ArrayList iconicList)
        {
            HRegion rectangleSerraggio = new HRegion();

            rectangleSerraggio.GenRectangle2(centerRow + param.DeltaYRectDaTappo + param.AltezzaRect, centerColumn, 0, param.LarghezzaRect, param.AltezzaRect);

            iconicList.Add(new Utilities.ObjectToDisplay(rectangleSerraggio, "green", 2));

            iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(centerRow, centerColumn - delta, centerRow, centerColumn + delta), "cyan", 3));

            HMeasure MeasureHandle = null;

            int Width, Height;
            Img.GetImageSize(out Width, out Height);

            HTuple RowEdge_first, ColumnEdge_first, Amplitude_first, Distance_first;
            HTuple RowEdge_last, ColumnEdge_last, Amplitude_last, Distance_last;

            HTuple RowEdge_first_tuple = new HTuple();
            HTuple RowEdge_last_tuple = new HTuple();
            HTuple ColumnEdge_first_tuple = new HTuple();
            HTuple ColumnEdge_last_tuple = new HTuple();

            HImage ImgEmfatize = Img.Emphasize(7, 7, param.EmphasizeFactorx10 / 10.0);

            double deltaH = (param.AltezzaRect * 2) / (param.NumLinee - 1);

            for (int i = 0; i < param.NumLinee; i++)
            {
                double row = centerRow + param.DeltaYRectDaTappo + (i * deltaH);

                if (row < Height && row > 0)
                {
                    // Misura degli edge sul primo rettangolo
                    MeasureHandle = new HMeasure(row, centerColumn, 0, param.LarghezzaRect, deltaH, Width, Height, "nearest_neighbor");
                    MeasureHandle.MeasurePos(ImgEmfatize, param.Sigmax10 / 10.0, param.Thresholdx10 / 10.0, "all", "first", out RowEdge_first, out ColumnEdge_first, out Amplitude_first, out Distance_first);
                    MeasureHandle.Dispose();        // Libera lo spazio associato a 'MeasureHandle'

                    MeasureHandle = new HMeasure(row, centerColumn, Math.PI, param.LarghezzaRect, deltaH, Width, Height, "nearest_neighbor");
                    MeasureHandle.MeasurePos(ImgEmfatize, param.Sigmax10 / 10.0, param.Thresholdx10 / 10.0, "all", "first", out RowEdge_last, out ColumnEdge_last, out Amplitude_last, out Distance_last);
                    MeasureHandle.Dispose();        // Libera lo spazio associato a 'MeasureHandle'

                    if (RowEdge_first.Length == 1 && RowEdge_last.Length == 1 && ColumnEdge_first.Length == 1 && ColumnEdge_last.Length == 1)
                    {
                        RowEdge_first_tuple = RowEdge_first_tuple.TupleConcat(RowEdge_first);
                        RowEdge_last_tuple = RowEdge_last_tuple.TupleConcat(RowEdge_last);
                        ColumnEdge_first_tuple = ColumnEdge_first_tuple.TupleConcat(ColumnEdge_first);
                        ColumnEdge_last_tuple = ColumnEdge_last_tuple.TupleConcat(ColumnEdge_last);
                    }
                }
            }
            ImgEmfatize.Dispose();

            //for (int i = 0; i < RowEdge_first_tuple.Length; i++)
            //{
            //    iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(RowEdge_first_tuple.DArr[i], ColumnEdge_first_tuple.DArr[i], 15, Math.PI / 4), "magenta", 1));
            //}

            //for (int i = 0; i < RowEdge_last_tuple.Length; i++)
            //{
            //    iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(RowEdge_last_tuple.DArr[i], ColumnEdge_last_tuple.DArr[i], 15, Math.PI / 4), "blue", 1));
            //}

            if (!(RowEdge_first_tuple.Length >= 2 && ColumnEdge_first_tuple.Length >= 2 && RowEdge_last_tuple.Length >= 2 && ColumnEdge_last_tuple.Length >= 2))
            {
                return false;
            }
            else
            {
                double x1First, y1First, x2First, y2First;

                LinearRegression(RowEdge_first_tuple, ColumnEdge_first_tuple, out x1First, out y1First, out x2First, out y2First);
                iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(y1First, x1First, y2First, x2First), "magenta", 2));

                double x1Last, y1Last, x2Last, y2Last;
                LinearRegression(RowEdge_last_tuple, ColumnEdge_last_tuple, out x1Last, out y1Last, out x2Last, out y2Last);
                iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(y1Last, x1Last, y2Last, x2Last), "blue", 2));

                HTuple rowRientranzeFirst, columnRientranzeFirst;
                int nRientranzeFirs;
                GetRientranze(RowEdge_first_tuple, ColumnEdge_first_tuple, x1First, y1First, x2First, y2First, param, out rowRientranzeFirst, out columnRientranzeFirst, out nRientranzeFirs, ref iconicList);

                HTuple rowRientranzeLast, columnRientranzeLast;
                int nRientranzeLast;
                GetRientranze(RowEdge_last_tuple, ColumnEdge_last_tuple, x1Last, y1Last, x2Last, y2Last, param, out rowRientranzeLast, out columnRientranzeLast, out nRientranzeLast, ref iconicList);

                for (int i = 0; i < nRientranzeFirs; i++)
                {
                    int row = (int)rowRientranzeFirst.DArr[i];
                    int column = (int)columnRientranzeFirst.DArr[i];

                    iconicList.Add(new Utilities.ObjectToDisplay((i + 1).ToString(), "green", row, column));
                    iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(row, column, 15, Math.PI / 4), "magenta", 1));
                    iconicList.Add(new Utilities.ObjectToDisplay("Rectangle1", new HTuple((double)row - 15, (double)column - 15, (double)row + 15, (double)column + 15), "magenta", 1));
                }

                for (int i = 0; i < nRientranzeLast; i++)
                {
                    int row = (int)rowRientranzeLast.DArr[i];
                    int column = (int)columnRientranzeLast.DArr[i];

                    iconicList.Add(new Utilities.ObjectToDisplay((i + 1 + nRientranzeFirs).ToString(), "green", row, column));
                    iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(row, column, 15, Math.PI / 4), "blue", 1));
                    iconicList.Add(new Utilities.ObjectToDisplay("Rectangle1", new HTuple((double)row - 15, (double)column - 15, (double)row + 15, (double)column + 15), "blue", 1));
                }

                return (nRientranzeFirs + nRientranzeLast) >= param.NumMinRientranze;
            }
        }

        private void LinearRegression(HTuple RowEdge, HTuple ColumnEdge, out double x1, out double y1, out double x2, out double y2)
        {
            double Nr2, Nc2, Dist2;
            HXLDCont cont = new HXLDCont();
            cont.GenContourPolygonXld(RowEdge, ColumnEdge);
            cont.FitLineContourXld("tukey", -1, 0, 5, 2.0, out y1, out x1, out y2, out x2, out Nr2, out Nc2, out Dist2);
        }

        private void GetRientranze(HTuple rowEdge, HTuple columnEdge, double x1, double y1, double x2, double y2, DataType.SerraggioStelvinParam param, out HTuple rowRientranze, out HTuple columnRientranze, out int numRientranze, ref ArrayList iconicList)
        {

            rowRientranze = new HTuple();
            columnRientranze = new HTuple();
            numRientranze = 0;

            double rowPrec = 0, columnPrec = 0;


            double rowTmp = 0, columnTmp = 0;
            int cnt = 0;

            int sogliaDivisioneRientranze = param.DistanzaMinRientranza * 3;

            for (int i = 0; i < rowEdge.Length; i++)
            {
                double row = rowEdge.DArr[i];
                double column = columnEdge.DArr[i];

                double d = HMisc.DistancePl(row, column, y1, x1, y2, x2);
                if (d > param.DistanzaMinRientranza)
                {
                    double distPrec = HMisc.DistancePp(rowPrec, columnPrec, row, column);
                    if (distPrec > sogliaDivisioneRientranze)
                    {
                        if (rowTmp != 0 && columnTmp != 0)
                        {
                            rowRientranze = rowRientranze.TupleConcat(rowTmp / cnt);
                            columnRientranze = columnRientranze.TupleConcat(columnTmp / cnt);
                        }

                        numRientranze++;
                        rowTmp = row;
                        columnTmp = column;
                        cnt = 1;
                    }
                    else
                    {
                        rowTmp += row;
                        columnTmp += column;
                        cnt++;
                    }

                    rowPrec = row;
                    columnPrec = column;

                    iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(row, column, 15, Math.PI / 4), "red", 1));

                }
            }
            if (rowTmp != 0 && columnTmp != 0)
            {
                rowRientranze = rowRientranze.TupleConcat(rowTmp / cnt);
                columnRientranze = columnRientranze.TupleConcat(columnTmp / cnt);
            }
        }

        #endregion serraggio stelvin

        #region piantaggio

        public void RegolazioniPiantaggioTappo(HImage[] images, CancellationToken token, out ArrayList[] iconicList, out ElaborateResult[] result)
        {
            iconicList = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            result = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            ArrayList[] iconicListArr = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            ElaborateResult[] resultArr = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            Parallel.For(0, Properties.Settings.Default.NumeroCamereTappo, i =>
            {

                ArrayList iconicTmp = new ArrayList();
                iconicListArr[i] = iconicTmp;
                resultArr[i] = new ElaborateResult();

                try
                {

                    iconicTmp.Add(new Utilities.ObjectToDisplay(images[i]));

                    if (this.parametri.AbilitaPiantaggio)
                    {
                        double centerO = 0;
                        double centerV = 0;

                        FindPositionBottle(images[i], this.parametri, false, out centerO, out centerV, ref iconicTmp);

                        bool ok = AnalisiPiantaggioTappo(images[i], this.parametri.Piantaggio, parametri.UsaCentraggioPET, centerO, centerV, ref iconicTmp);

                        resultArr[i].Success = ok;
                    }
                    else
                    {
                        iconicTmp.Add(new Utilities.ObjectToDisplay(linguaMngr.GetTranslation("MSG_TEST_DISABLED"), "red", 0, 0));

                        resultArr[i].Success = true;
                    }
                }
                catch (Exception ex)
                {
                    ExceptionManager.AddException(ex);
                    resultArr[i].Success = false;
                }
            });

            for (int i = 0; i < Properties.Settings.Default.NumeroCamereTappo; i++)
            {
                iconicList[i] = iconicListArr[i];
                result[i] = resultArr[i];
            }

        }

        private bool AnalisiPiantaggioTappo(HImage Img, DataType.PiantaggioParam param, bool usaCentraggioPET, double centerColumn, double centerRow, ref ArrayList iconicList)
        {
            bool ret = false;

            double rowMax = 0;
            double rowMin = 0;

            if (!usaCentraggioPET)
            {
                rowMax = param.RowMax;
                rowMin = param.RowMin;
            }
            else
            {
                rowMax = centerRow - param.RowMax;
                rowMin = centerRow - param.RowMin;
            }

            iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(rowMax, centerColumn - delta, rowMax, centerColumn + delta), "cyan", 3));
            iconicList.Add(new Utilities.ObjectToDisplay("Max", "cyan", (int)(rowMax - delta), (int)(centerColumn + 2 * delta)));

            iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(rowMin, centerColumn - delta, rowMin, centerColumn + delta), "cyan", 3));
            iconicList.Add(new Utilities.ObjectToDisplay("Min", "cyan", (int)(rowMin - delta), (int)(centerColumn + 2 * delta)));

            if (centerRow > 0)
            {

                HRegion rectangleCentraggio = new HRegion();

                int Width, Height;
                Img.GetImageSize(out Width, out Height);

                //if (!usaCentraggioPET)
                rectangleCentraggio.GenRectangle2(Height / 2, centerColumn, Math.PI / 2, Height / 2, param.LarghezzaControllo / 2);
                //else
                //    rectangleCentraggio.GenRectangle2(0, centerColumn, Math.PI / 2, centerRow, param.LarghezzaControllo / 2);

                iconicList.Add(new Utilities.ObjectToDisplay(rectangleCentraggio, "red", 1));

                double altezzaTappo = 0;

                double deltaRow = param.LarghezzaControllo / (param.NumControlli + 1);

                double altezzaMax = double.MaxValue;
                double altezzaMin = 0;

                for (int i = 0; i < param.NumControlli; i++)
                {
                    HTuple rowEdge, columnEdge, amplitude, distance;

                    double colonna = centerColumn - param.LarghezzaControllo / 2 + (i + 1) * deltaRow;

                    // Misura degli edge sul primo rettangolo
                    HMeasure measureHandle = null;

                    int dir = param.AltoVersoBasso ? -1 : 1;

                    //if (!usaCentraggioPET)
                    measureHandle = new HMeasure(Height / 2, colonna, dir * Math.PI / 2, Height / 2, 15, Width, Height, "nearest_neighbor");
                    //else
                    //    measureHandle = new HMeasure(0, colonna, dir * Math.PI / 2, centerRow, 15, Width, Height, "nearest_neighbor");

                    measureHandle.MeasurePos(Img, param.Sigma, param.Threshold, "all", "first", out rowEdge, out columnEdge, out amplitude, out distance);
                    measureHandle.Dispose();

                    if (rowEdge.Length > 0)
                    {
                        double altezzaTmp = rowEdge.D;

                        if (altezzaTmp < altezzaMax)
                            altezzaMax = altezzaTmp;

                        if (altezzaTmp > altezzaMin)
                            altezzaMin = altezzaTmp;

                        iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(rowEdge.D, colonna, 15, Math.PI / 4), "magenta", 1));
                    }
                }

                altezzaTappo = altezzaMax;

                //iconicList.Add(new Utilities.ObjectToDisplay(string.Format("Delta misure tappo = {0}", altezzaMin - altezzaMax), "red", 0, 0));

                iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(altezzaTappo, centerColumn, 15, Math.PI / 4), "green", 1));

                if (param.MaxDeltaAltezze < (altezzaMin - altezzaMax))
                {
                    ret = false;
                }
                else
                {
                    if (altezzaTappo >= rowMax && altezzaTappo <= rowMin)
                        ret = true;
                    else
                        ret = false;

                    iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(altezzaTappo, centerColumn - delta, altezzaTappo, centerColumn + delta), ret ? "green" : "red", 3));

                }

            }
            else
            {
                ret = false;
            }

            return ret;
        }

        #endregion piantaggio

        #region ControlloAnello

        public void RegolazioniControlloAnello(HImage[] images, CancellationToken token, out ArrayList[] iconicList, out ElaborateResult[] result)
        {
            iconicList = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            result = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            ArrayList[] iconicListArr = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            ElaborateResult[] resultArr = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            Parallel.For(0, Properties.Settings.Default.NumeroCamereTappo, i =>
            {
                ArrayList iconicTmp = new ArrayList();
                iconicListArr[i] = iconicTmp;
                resultArr[i] = new ElaborateResult();

                try
                {

                    iconicTmp.Add(new Utilities.ObjectToDisplay(images[i]));

                    if (this.parametri.AbilitaControlloAnello)
                    {
                        double centerO = 0;
                        double centerV = 0;

                        FindPositionBottle(images[i], this.parametri, false, out centerO, out centerV, ref iconicTmp);

                        bool ok = AnalisiControlloAnello(images[i], this.parametri.ControlloAnello, centerO, centerV, true, ref iconicTmp);

                        resultArr[i].Success = ok;
                    }
                    else
                    {
                        iconicTmp.Add(new Utilities.ObjectToDisplay(linguaMngr.GetTranslation("MSG_TEST_DISABLED"), "red", 0, 0));

                        resultArr[i].Success = true;
                    }
                }
                catch (Exception ex)
                {
                    ExceptionManager.AddException(ex);
                    resultArr[i].Success = false;
                }
            });

            for (int i = 0; i < Properties.Settings.Default.NumeroCamereTappo; i++)
            {
                iconicList[i] = iconicListArr[i];
                result[i] = resultArr[i];
            }

        }

        private bool AnalisiControlloAnello(HImage Img, DataType.ControlloAnelloParam param, double centerColumn, double centerRow, bool regolazioni, ref ArrayList iconicList)
        {
            bool ret = false;

            HRegion rectangleSerraggio = new HRegion();

            rectangleSerraggio.GenRectangle2(centerRow + param.DeltaYRectDaTappo + param.AltezzaRect, centerColumn, 0, param.LarghezzaRect, param.AltezzaRect);

            HImage imageReduced = Img.ReduceDomain(rectangleSerraggio);
            HRegion regionThr = imageReduced.Threshold((double)param.ThresholdMin, (double)param.ThresholdMax);
            imageReduced.Dispose();
            HRegion connectedRegions = regionThr.Connection();
            regionThr.Dispose();
            HRegion unionRegion = connectedRegions.Union1();
            connectedRegions.Dispose();

            if (unionRegion.Area < param.Soglia)
            {
                ret = true;
            }

            //iconicList.Add(new Utilities.ObjectToDisplay(string.Format("Area = {0}", unionRegion.Area.D), "red", 200, 0));

            iconicList.Add(new Utilities.ObjectToDisplay(rectangleSerraggio, "green", 2));
            iconicList.Add(new Utilities.ObjectToDisplay(unionRegion, ret ? "green" : "red", 2) { DrawMode = "fill" });

            int TextRow = Convert.ToInt16(centerRow + param.DeltaYRectDaTappo + param.AltezzaRect - 16);
            int TextColumn = Convert.ToInt16(centerColumn - 8);

            iconicList.Add(new Utilities.ObjectToDisplay("√", ret ? "green" : "red", TextRow, TextColumn));

            if (regolazioni)
            {
                HTuple absoluteHisto, relativeHisto;
                absoluteHisto = Img.GrayHisto(rectangleSerraggio, out relativeHisto);

                int totalPixel = absoluteHisto.TupleSum();

                int indexStart = param.ThresholdMin;
                int indexStop = param.ThresholdMax;

                long sommaPixel = 0;

                if ((indexStart < absoluteHisto.LArr.Length) && (indexStop < absoluteHisto.LArr.Length))
                {
                    for (int i = indexStart; i <= indexStop; i++)
                    {
                        sommaPixel += absoluteHisto.LArr[i];
                    }
                }

                // In regolazioni visualizzo l'histogramma colore
                HFunction1D function = new HFunction1D(absoluteHisto);
                HFunction1D smoothedFunction = function.SmoothFunct1dGauss(2.0);
                HTuple xValues, yValues;
                smoothedFunction.Funct1dToPairs(out xValues, out yValues);

                HRegion histoRegion2 = new HRegion();

                const int TableRow = 256;
                const int TableColumn = 256;

                histoRegion2.GenRegionHisto(yValues, TableRow, TableColumn, 1);

                iconicList.Add(new Utilities.ObjectToDisplay(histoRegion2, "green", 1));

                int textRow = Convert.ToInt16(TableRow + TableColumn * 0.5);
                int textColumn1 = 128 + indexStart;
                int textColumn2 = 128 + indexStop;

                iconicList.Add(new Utilities.ObjectToDisplay("|", "red", textRow - 50, textColumn1));
                iconicList.Add(new Utilities.ObjectToDisplay("|", "red", textRow - 50, textColumn2));
                iconicList.Add(new Utilities.ObjectToDisplay(sommaPixel.ToString(), "red", TableRow, textColumn1));

            }

            return ret;
        }

        #endregion ControlloAnello

        #region Gabbietta

        public void RegolazioniGabbietta(HImage[] images, CancellationToken token, out ArrayList[] iconicList, out ElaborateResult[] result)
        {
            iconicList = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            result = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            ArrayList[] iconicListArr = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            ElaborateResult[] resultArr = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            Parallel.For(0, Properties.Settings.Default.NumeroCamereTappo, i =>
            {

                ArrayList iconicTmp = new ArrayList();
                iconicListArr[i] = iconicTmp;
                resultArr[i] = new ElaborateResult();

                try
                {

                    iconicTmp.Add(new Utilities.ObjectToDisplay(images[i]));

                    if (this.parametri.AbilitaControlloGabbietta)
                    {
                        double centerO = 0;
                        double centerV = 0;

                        FindPositionBottle(images[i], this.parametri, false, out centerO, out centerV, ref iconicTmp);

                        bool ok = AnalisiGabbietta(images[i], this.parametri.Gabbietta, parametri.UsaCentraggioPET, centerO, centerV, ref iconicTmp);

                        resultArr[i].Success = ok;
                    }
                    else
                    {
                        iconicTmp.Add(new Utilities.ObjectToDisplay(linguaMngr.GetTranslation("MSG_TEST_DISABLED"), "red", 0, 0));

                        resultArr[i].Success = true;
                    }
                }
                catch (Exception ex)
                {
                    ExceptionManager.AddException(ex);
                    resultArr[i].Success = false;
                }
            });

            for (int i = 0; i < Properties.Settings.Default.NumeroCamereTappo; i++)
            {
                iconicList[i] = iconicListArr[i];
                result[i] = resultArr[i];
            }

        }

        private bool AnalisiGabbietta(HImage Img, DataType.GabbiettaParam param, bool usaCentraggioPET, double centerColumn, double centerRow, ref ArrayList iconicList)
        {
            bool ret = false;

            double rowMax = 0;
            double rowMin = 0;

            if (!usaCentraggioPET)
            {
                rowMax = param.RowMax;
                rowMin = param.RowMin;
            }
            else
            {
                rowMax = centerRow - param.RowMax;
                rowMin = centerRow - param.RowMin;
            }

            iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(rowMax, centerColumn - delta, rowMax, centerColumn + delta), "cyan", 3));
            iconicList.Add(new Utilities.ObjectToDisplay("Max", "cyan", (int)(rowMax - delta), (int)(centerColumn + 2 * delta)));

            iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(rowMin, centerColumn - delta, rowMin, centerColumn + delta), "cyan", 3));
            iconicList.Add(new Utilities.ObjectToDisplay("Min", "cyan", (int)(rowMin - delta), (int)(centerColumn + 2 * delta)));

            if (centerRow > 0)
            {

                HRegion rectangleCentraggio = new HRegion();

                int Width, Height;
                Img.GetImageSize(out Width, out Height);

                //if (!usaCentraggioPET)
                rectangleCentraggio.GenRectangle2(Height / 2, centerColumn, Math.PI / 2, Height / 2, param.LarghezzaControllo / 2);
                //else
                //    rectangleCentraggio.GenRectangle2(0, centerColumn, Math.PI / 2, centerRow, param.LarghezzaControllo / 2);

                iconicList.Add(new Utilities.ObjectToDisplay(rectangleCentraggio, "red", 1));

                double altezzaTappo = 0;

                double deltaRow = param.LarghezzaControllo / (param.NumControlli + 1);

                double altezzaMax = double.MaxValue;
                double altezzaMin = 0;

                for (int i = 0; i < param.NumControlli; i++)
                {
                    HTuple rowEdge, columnEdge, amplitude, distance;

                    double colonna = centerColumn - param.LarghezzaControllo / 2 + (i + 1) * deltaRow;

                    // Misura degli edge sul primo rettangolo
                    HMeasure measureHandle = null;

                    int dir = param.AltoVersoBasso ? -1 : 1;

                    //if (!usaCentraggioPET)
                    measureHandle = new HMeasure(Height / 2, colonna, dir * Math.PI / 2, Height / 2, 15, Width, Height, "nearest_neighbor");
                    //else
                    //    measureHandle = new HMeasure(0, colonna, dir * Math.PI / 2, centerRow, 15, Width, Height, "nearest_neighbor");

                    measureHandle.MeasurePos(Img, param.Sigma, param.Threshold, "all", "first", out rowEdge, out columnEdge, out amplitude, out distance);
                    measureHandle.Dispose();

                    if (rowEdge.Length > 0)
                    {
                        double altezzaTmp = rowEdge.D;

                        if (altezzaTmp < altezzaMax)
                            altezzaMax = altezzaTmp;

                        if (altezzaTmp > altezzaMin)
                            altezzaMin = altezzaTmp;

                        iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(rowEdge.D, colonna, 15, Math.PI / 4), "magenta", 1));
                    }
                }

                altezzaTappo = altezzaMax;

                //iconicList.Add(new Utilities.ObjectToDisplay(string.Format("Delta misure tappo = {0}", altezzaMin - altezzaMax), "red", 0, 0));

                iconicList.Add(new Utilities.ObjectToDisplay("Cross", new HTuple(altezzaTappo, centerColumn, 15, Math.PI / 4), "green", 1));

                if (param.MaxDeltaAltezze < (altezzaMin - altezzaMax))
                {
                    ret = false;
                }
                else
                {
                    if (altezzaTappo >= rowMax && altezzaTappo <= rowMin)
                        ret = true;
                    else
                        ret = false;

                    iconicList.Add(new Utilities.ObjectToDisplay("Line", new HTuple(altezzaTappo, centerColumn - delta, altezzaTappo, centerColumn + delta), ret ? "green" : "red", 3));

                }

            }
            else
            {
                ret = false;
            }

            return ret;
        }

        #endregion Gabbietta

        public void WorkingControlloTappoAlgorithm(HImage[] images, CancellationToken token, out ArrayList[] iconicList, out ElaborateResult[] result)
        {
            iconicList = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            result = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            ArrayList[] iconicListArr = new ArrayList[Properties.Settings.Default.NumeroCamereTappo];
            ElaborateResult[] resultArr = new ElaborateResult[Properties.Settings.Default.NumeroCamereTappo];

            try
            {

                Parallel.For(0, Properties.Settings.Default.NumeroCamereTappo, i =>
                //for (int i = 0; i < Properties.Settings.Default.NumeroCamereTappo; i++)
                {

                    ArrayList iconicTmp = new ArrayList();
                    iconicListArr[i] = iconicTmp;
                    resultArr[i] = new ElaborateResult();

                    try
                    {

                        bool ok = true;

                        bool errorAnalisiPresenzaTappo = false;
                        bool errorAnalisiSerraggioTappo = false;
                        bool errorAnalisiSerraggioStelvinTappo = false;
                        bool errorAnalisiPiantaggioTappo = false;
                        bool errorAnalisiControlloAnello = false;
                        bool errorAnalisiGabbietta = false;
                        bool errorLevelMin = false;
                        bool errorLevelMax = false;
                        bool errorEmpty = false;


                        iconicTmp.Add(new Utilities.ObjectToDisplay(images[i]));

                        if (this.parametri == null)
                        {
                            iconicTmp.Add(new Utilities.ObjectToDisplay(linguaMngr.GetTranslation("MSG_NO_RECIPE"), "red", 0, 0));
                        }
                        else
                        {
                            double centerO = 0;
                            double centerV = 0;

                            if (this.parametri.AbilitaPresenza || this.parametri.AbilitaSerraggio || this.parametri.AbilitaSerraggioStelvin || this.parametri.AbilitaPiantaggio || this.parametri.AbilitaControlloAnello || this.parametri.AbilitaControlloGabbietta)
                            {
                                centerO = 0;
                                centerV = 0;

                                FindPositionBottle(images[i], this.parametri, false, out centerO, out centerV, ref iconicTmp);
                            }

                            token.ThrowIfCancellationRequested();

                            if (this.parametri.AbilitaPresenza && ok)
                            {
                                bool ok_ = AnalisiPresenzaTappo(images[i], this.parametri.Presenza, centerO, centerV, false, ref iconicTmp);

                                errorAnalisiPresenzaTappo = !ok_;

                                if (ok)
                                    ok = ok_;
                            }

                            token.ThrowIfCancellationRequested();

                            if (this.parametri.AbilitaSerraggio && ok)
                            {
                                bool ok_ = AnalisiSerraggioTappo(images[i], this.parametri.Serraggio, centerO, centerV, ref iconicTmp);

                                errorAnalisiSerraggioTappo = !ok_;

                                if (ok)
                                    ok = ok_;
                            }

                            token.ThrowIfCancellationRequested();

                            if (this.parametri.AbilitaSerraggioStelvin && ok)
                            {
                                bool ok_ = AnalisiSerraggioStelvinTappo(images[i], this.parametri.SerraggioStelvin, centerO, centerV, ref iconicTmp);

                                errorAnalisiSerraggioStelvinTappo = !ok_;

                                if (ok)
                                    ok = ok_;
                            }

                            token.ThrowIfCancellationRequested();

                            if (this.parametri.AbilitaPiantaggio && ok)
                            {
                                bool ok_ = AnalisiPiantaggioTappo(images[i], this.parametri.Piantaggio, parametri.UsaCentraggioPET, centerO, centerV, ref iconicTmp);

                                errorAnalisiPiantaggioTappo = !ok_;

                                if (ok)
                                    ok = ok_;
                            }

                            token.ThrowIfCancellationRequested();

                            if (this.parametri.AbilitaControlloAnello && ok)
                            {
                                bool ok_ = AnalisiControlloAnello(images[i], this.parametri.ControlloAnello, centerO, centerV, false, ref iconicTmp);

                                errorAnalisiControlloAnello = !ok_;

                                if (ok)
                                    ok = ok_;
                            }

                            token.ThrowIfCancellationRequested();

                            if (this.parametri.AbilitaControlloGabbietta && ok)
                            {
                                bool ok_ = AnalisiGabbietta(images[i], this.parametri.Gabbietta, parametri.UsaCentraggioPET, centerO, centerV, ref iconicTmp);

                                errorAnalisiGabbietta = !ok_;

                                if (ok)
                                    ok = ok_;
                            }

                            //if (this.parametri.AbilitaControlloLivello && ok)
                            if (this.parametri.AbilitaControlloLivello)   // |MP 28-1-19 nonentrava xchè ok=false
                            {
                                bool ok_ = AnalisiLivello(images[i], this.parametri.Livello, centerO, true, out errorLevelMin, out errorLevelMax, out errorEmpty, ref iconicTmp);

                                if (!ok_)
                                {
                                    ok = ok_;
                                    errorEmpty = !ok_;  // |MP 28-1-19 è true quando passa di qua
                                }
                            }
                        }

                        resultArr[i].Success = ok;

                        resultArr[i].DettaglioElaborazione.Add("KO_CAP_PRESENCE", errorAnalisiPresenzaTappo);
                        resultArr[i].DettaglioElaborazione.Add("KO_CAP_CLAMPING", errorAnalisiSerraggioTappo);
                        resultArr[i].DettaglioElaborazione.Add("KO_CAP_STELVIN", errorAnalisiSerraggioStelvinTappo);
                        resultArr[i].DettaglioElaborazione.Add("KO_CAP_POSITION", errorAnalisiPiantaggioTappo);
                        resultArr[i].DettaglioElaborazione.Add("KO_CAP_RING", errorAnalisiControlloAnello);
                        resultArr[i].DettaglioElaborazione.Add("KO_CAP_GABBIETTA", errorAnalisiGabbietta);
                        resultArr[i].DettaglioElaborazione.Add("KO_LEVEL_MIN", errorLevelMin);
                        resultArr[i].DettaglioElaborazione.Add("KO_LEVEL_MAX", errorLevelMax);
                        resultArr[i].DettaglioElaborazione.Add("KO_LEVEL_EMPTY", errorEmpty);
                    }
                    catch (Exception ex)
                    {
                        ExceptionManager.AddException(ex);
                        resultArr[i].Success = false;
                    }
                });

            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                for (int i = 0; i < Properties.Settings.Default.NumeroCamereTappo; i++)
                {
                    iconicList[i] = iconicListArr[i];
                    result[i] = resultArr[i];
                }
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
        ~AlgoritmoControlloTappo()
        {
            // Simply call Dispose(false).
            Dispose(false);
        }

        #endregion IDisposable

    }
}
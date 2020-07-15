using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdeSolverWPF
{
    class OdeSolver
    {
        //<summary> Метод Эйлера</summary>
        public static Double[][] Euler(Func<Double, Double[], Double[]> Equation, Double[] TimeSpan, Double[] x0)
        {
            Double h = TimeSpan[1] - TimeSpan[0];
            int k = TimeSpan.Length;
            Double[][] X = new Double[k][];

            X[0] = x0;

            Double[] buf;

            for (int i = 1; i < k; i++)
            {
                buf = Equation(TimeSpan[i], X[i - 1]);              
                X[i] = buf.Select((x, index) => X[i - 1][index] + h * x).ToArray();
                
            }

            return X;
        }

        //<summary> Метод Эйлера-Коши 2го порядка</summary>
        public static Double[][] EulerCauchy(Func<Double, Double[], Double[]> Equation, Double[] TimeSpan, Double[] x0)
        {
            Double h = TimeSpan[1] - TimeSpan[0];
            int k = TimeSpan.Length;
            Double[][] X = new Double[k][];

            X[0] = x0;

            Double[] A;
            Double[] B;
            for (int i = 1; i < k; i++)
            {
                A = Equation(TimeSpan[i - 1], X[i - 1]);
                B = Equation(TimeSpan[i], X[i - 1].Select((x, index) => x + h * A[index]).ToArray());
                X[i] = X[i - 1].Select((x, index) => x + 0.5 * h * (A[index] + B[index])).ToArray();
            }

            return X;
        }
        //<summary> Метод Рунге-Кутты 2 порядка /<summary>
        public static Double[][] RK2(Func<Double, Double[], Double[]> Equation, Double[] TimeSpan, Double[] x0)
        {

            Double h = TimeSpan[1] - TimeSpan[0];
            int k = TimeSpan.Length;
            Double[][] X = new Double[k][];

            X[0] = x0;

            Double[] F1;
            Double[] F2;
            for (int i = 1; i < k; i++)
            {
                F1 = Equation(TimeSpan[i - 1], X[i - 1]);
                F2 = Equation(TimeSpan[i - 1] + h / 2, F1.Select((x, index) => x * h / 2 + X[i - 1][index]).ToArray());
                X[i] = F2.Select((x, index) => X[i - 1][index] + h * x).ToArray();
            }

            return X;
        }
        //<summary> Метод Рунге-Кутты 4 порядка </summary>
        public static Double[][] RK4(Func<Double, Double[], Double[]> Equation, Double[] TimeSpan, Double[] x0)
        {

            Double h = TimeSpan[1] - TimeSpan[0];
            int k = TimeSpan.Length;
            Double[][] X = new Double[k][];

            X[0] = x0;

            Double[] K1;
            Double[] K2;
            Double[] K3;
            Double[] K4;
            for (int i = 1; i < k; i++)
            {
                K1 = Equation(TimeSpan[i - 1], X[i - 1]);
                K2 = Equation(TimeSpan[i - 1] + h / 2, K1.Select((x, index) => X[i - 1][index] + x * h / 2).ToArray());
                K3 = Equation(TimeSpan[i - 1] + h / 2, K2.Select((x, index) => X[i - 1][index] + x * h / 2).ToArray());
                K4 = Equation(TimeSpan[i - 1] + h, K3.Select((x, index) => X[i - 1][index] + x * h).ToArray());

                X[i] = K4.Select((x, index) => h / 6 * (K1[index] + 2 * K2[index] + 2 * K3[index] + x) + X[i - 1][index]).ToArray();

            }

            return X;
        }

        public static Double[][] RKF45(Func<Double, Double[], Double[]> Equation, Double[] Timespan, Double[] x0)
        {
            Double Tollerance = 1e-6;

            Double h = Timespan[1] - Timespan[0];
            int k = Timespan.Length;
            Double[][] X = new Double[k][];

            X[0] = x0;

            Double[] K1, K2, K3, K4, K5, K6;


            Double[] Errors;

            Double MaxError, S;

            for (int i = 1; i < k; i++)
            {
                do
                {
                    K1 = Equation(Timespan[i - 1], X[i - 1]).Select(x => x * h).ToArray();

                    K2 = Equation(Timespan[i - 1] + h / 4, K1.Select((x, index) => X[i - 1][index] + x / 4).ToArray());
                    K2 = K2.Select(x => x * h).ToArray();

                    K3 = Equation(Timespan[i - 1] + (3.0 / 8.0) * h,
                        K2.Select((x, index) => X[i - 1][index] + 3.0 / 32.0 * K1[index] + 9.0 / 32.0 * x).ToArray());
                    K3 = K3.Select(x => x * h).ToArray();


                    K4 = Equation(Timespan[i - 1] + 12.0 / 13.0 * h,
                        K3.Select((x, index) => X[i - 1][index] + 1932.0 / 2197.0 * K1[index]
                            - 7200.0 / 2197.0 * K2[index] + 7296.0 / 2197.0 * x).ToArray());
                    K4 = K4.Select(x => x * h).ToArray();

                    K5 = Equation(Timespan[i - 1] + h,
                        K4.Select((x, index) => X[i - 1][index] + 439.0 / 216.0 * K1[index] - 8 * K2[index]
                        + 3680.0 / 513.0 * K3[index] - 845.0 / 4104.0 * x).ToArray());
                    K5 = K5.Select(x => x * h).ToArray();

                    K6 = Equation(Timespan[i - 1] + h / 2,
                        K5.Select((x, index) => X[i - 1][index] - 8.0 / 27.0 * K1[index] + 2 * K2[index]
                        - 3544.0 / 2565.0 * K3[index] + 1859.0 / 4104.0 * K4[index] - 11.0 / 40.0 * x).ToArray());
                    K6 = K6.Select(x => x * h).ToArray();

                    Errors = K6.Select((x, index) => Math.Abs(K1[index] / 360 - 128.0 / 4275.0 * K3[index] - 2197.0 / 75240.0 * K4[index]
                        + K5[index] / 50 + 2.0 / 55.0 * x) / h).ToArray();

                    MaxError = Errors.Max();
                    S = Math.Pow(0.5 * Tollerance / MaxError, 0.25);

                    h = h * S;

                } while (MaxError > Tollerance);

                //5th order
                X[i] = K6.Select((x, index) => X[i - 1][index] + 16.0 / 135.0 * K1[index] + 6656.0 / 12825.0 * K3[index]
                    + 28561.0 / 56430.0 * K4[index] - 9.0 / 50.0 * K5[index] + 2.0 / 55.0 * x).ToArray();
            }
            return X;
        }

    }
}

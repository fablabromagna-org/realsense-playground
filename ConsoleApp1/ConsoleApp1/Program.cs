using Intel.RealSense;
using System;
using System.Linq;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            const int RISOLUZIONE_VERTICALE = 20;
            const int RISOLUZIONE_ORIZZONTALE = 10;

            //const int W = 1280;
            //const int H = 720;
            //const int W = 480;
            //const int H = 270;

            const int W = 640;
            const int H = 480;
            const int COLONNE = W / RISOLUZIONE_ORIZZONTALE;
            const int RIGHE = H / RISOLUZIONE_VERTICALE;

            FrameQueue q = new FrameQueue();

            using (var ctx = new Context())
            {
                var devices = ctx.QueryDevices();

                if (devices.Count == 0) {
                    Console.WriteLine("Nessuna RealSense trovata nel sistema!");
                    return;
                }

                string msg = devices.Count > 1 ?
                    $"Trovate {devices.Count} RealSense collegate." :
                    $"Trovata una RealSense collegata.";
;
                Console.WriteLine(msg);

                var dev = devices[0];
                Console.WriteLine("\nPrendo la prima:\n");
                Console.WriteLine("Nome: {0}", dev.Info[CameraInfo.Name]);
                Console.WriteLine("Numero di serie: {0}", dev.Info[CameraInfo.SerialNumber]);
                Console.WriteLine("Versione del firmware: {0}", dev.Info[CameraInfo.FirmwareVersion]);
                Console.WriteLine("Numero di sensori presenti: {0}\n", dev.Sensors.Count);

                int sensoreCnt = 1;
                foreach (var sensore in dev.Sensors)
                {
                    Console.WriteLine($"Caratteristiche del sensore {sensoreCnt}: {sensore.Instance}");
                    foreach (var opzione in sensore.Options)
                        Console.WriteLine($"{opzione.Description}: {opzione.Value}");

                    Console.WriteLine($"\nAspect ratio del sensore {sensoreCnt}: {sensore.Instance}");
                    foreach (var profilo in sensore.VideoStreamProfiles)
                        Console.WriteLine($"{profilo.Width}x{profilo.Height} {profilo.Stream} framerate: {profilo.Framerate}");

                    Console.WriteLine("\n---\n");
                    sensoreCnt++;
                }

                Console.ReadKey();
                Console.Clear();

                var depthSensor = dev.Sensors[0];
                var sp = depthSensor.VideoStreamProfiles
                                    .Where(p => p.Stream == Stream.Depth)
                                    .OrderByDescending(p => p.Framerate)
                                    .Where(p => p.Width == W && p.Height == H)
                                    .First();
                depthSensor.Open(sp);
                depthSensor.Start(q);

                int one_meter = (int)(1f / depthSensor.DepthScale);

                var run = true;
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    run = false;
                };

                ushort[] depth = new ushort[sp.Width * sp.Height];

                while (run)
                {
                    using (var f = q.WaitForFrame() as VideoFrame)
                    {
                        f.CopyTo(depth);
                    }

                    var buffer = new char[(W / RISOLUZIONE_ORIZZONTALE + 1) * (H / RISOLUZIONE_VERTICALE)];
                    var coverage = new int[COLONNE]; // ogni cella conterrà l'indice del carattere da visualizzare (proporzionale alla profondità)
                    int b = 0;

                    for (int y = 0; y < H; ++y)
                    {
                        // per ogni riga y
                        for (int x = 0; x < W; ++x)
                        {
                            // per ogni colonna x
                            ushort d = depth[x + y * W];    // prendi il valore della profondità dalla videocamera
                            if (d > 0 && d < one_meter)     // se stiamo sotto al metro
                                coverage[x / RISOLUZIONE_ORIZZONTALE]++;         // leggendo 640pixel, riempiamo 64 celle
                        }

                        if (y % RISOLUZIONE_VERTICALE == RISOLUZIONE_VERTICALE-1)
                        {
                            // Ogni riga
                            for (int i = 0; i < coverage.Length; i++)
                            {
                                int c = coverage[i];
                                buffer[b++] = " .-═║░▒▓█"[c / (RIGHE+1)]; 
                                coverage[i] = 0;
                            }
                            buffer[b++] = '\n';
                        }
                    }

                    Console.SetCursorPosition(0, 0);
                    Console.WriteLine();
                    Console.Write(buffer);
                }

                depthSensor.Stop();
                depthSensor.Close();
            }

        }
    }
}

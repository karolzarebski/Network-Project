﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Klient
{
    class Program
    {
        // Pola operacji  
        private static string WYSLANIE_ID = "000010";
        private static string LString = "001000";
        private static string ODP_SERWERA = "100000";
        private static string ODP_KLIENTA = "000100";
        private static string KONIEC_POLACZENIA = "100010";

        // Pola odpowiedzi
        private static string ODP_POCZATEK = "001";
        private static string ODP_KONIEC = "010";
        private static string ODP_ZGADLES = "100";
        private static string ODP_NIE_ZGADLES = "101";
        private static string ODP_LICZBA = "110";
        private static string ODP_DRUGI_KLIENT_ZGADL = "111";

        // OP-6 | ODP-3 | ID-3 | Liczba - 16

        private static TcpClient client;
        private static Stream stream;

        private static int przedzialP, przedzialK, port = 13000;
        private static bool koniec = false, ipOK = true, portOK = true, error = false;
        private static String ip = "192.168.1.100", idString;

        static void Main(string[] args)
        {
            do
            {
                Console.Write("Podaj IP : ");
                ip = Console.ReadLine();
                IPAddress ipA;
                ipOK = IPAddress.TryParse(ip, out ipA);
                if (!ipOK)
                {
                    Console.WriteLine("Błędny adres ip, spróbuj ponownie");
                }
            } while (!ipOK);

            do
            {
                Console.Write("Podaj port : ");
                try
                {
                    port = Convert.ToInt32(Console.ReadLine());
                    if (port >= 1024 && port <= 65535)
                    {
                        portOK = true;
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Błędny port, spróbuj ponownie");
                    portOK = false;
                }
            } while (!portOK);

            // Proba polaczenia
            try
            {
                client = new TcpClient(ip, port);
            }
            catch (Exception)
            {
                Console.WriteLine("Błąd połączenia \r\nWciśnij dowolny przycisk aby zakończyć program.");
                Console.Read();
                return;
            }
            stream = client.GetStream();

            while (!koniec)
                OdbierzDane();

            if (error)
            {
                return;
            }

            // Utworzenie pustego bufora danych
            string dane = string.Empty;

            // Dopisanie pola operacji
            dane += KONIEC_POLACZENIA;

            // Dopisanie pola odpowiedzi
            dane += "000";

            // Dopisanie ID
            dane += idString;

            // Dopisanie pustej liczby oraz dopelnienia
            dane += "000000000000000000000000";

            // Zamiania bitow na bajty
            byte[] daneB = new byte[] { (byte)Convert.ToInt32(dane.Substring(0, 8), 2), (byte)Convert.ToInt32(dane.Substring(8, 8), 2),
                                        (byte)Convert.ToInt32(dane.Substring(16, 8), 2), (byte)Convert.ToInt32(dane.Substring(24), 2)};

            // Proba wyslania pakietu z danymi
            try
            {
                stream.Write(daneB, 0, 4);
            }
            catch (Exception)
            {
                Console.WriteLine("Wystąpił błąd wysyłania danych.");
            }
            stream.Close();
            client.Close();
            Console.WriteLine("Wciśnij dowolny przycisk aby zakończyć program.");
            Console.Read();
        }
        static void OdbierzDane()
        {
            byte[] dane = new byte[4];

            // Proba odebrania 4 bajtow
            try
            {
                stream.Read(dane, 0, 4);
            }
            catch (Exception)
            {
                Console.WriteLine("Wystąpił błąd odczytu danych. \r\nWciśnij dowolny przycisk aby zakończyć program.");
                Console.Read();
                koniec = true;
                error = true;
                return;
            }

            // Zamiana bajtow na bity i polaczenie ich w jeden ciag
            string daneS = ZamienNaBinarny(dane[0]) + ZamienNaBinarny(dane[1]) + ZamienNaBinarny(dane[2]) + ZamienNaBinarny(dane[3]);

            // Podzial ciagu na poszczegolne pola
            string OPString = daneS.Substring(0, 6);
            string ODPString = daneS.Substring(6, 3);
            string IDString = daneS.Substring(9, 3);
            string liczbaString = daneS.Substring(12, 16);

            if (OPString == WYSLANIE_ID)
            {
                // Zapisanie ID wyslanego przez serwer
                idString = IDString;

                WyslijLiczbe(false);
                Console.WriteLine("Poczekaj na odpowiedź serwera");
            }
            else if (OPString == ODP_SERWERA)
            {
                if (ODPString == ODP_POCZATEK)
                {
                    // Zapisanie poczatku przedzialu
                    przedzialP = Convert.ToInt16(liczbaString, 2);
                }
                else if (ODPString == ODP_KONIEC)
                {
                    // Zapisanie konca przedzialu
                    przedzialK = Convert.ToInt16(liczbaString, 2);
                    WyslijLiczbe(true);
                }
                else if (ODPString == ODP_ZGADLES)
                {
                    Console.WriteLine("Brawo zgadles liczbe! :)");
                    koniec = true;
                }
                else if (ODPString == ODP_NIE_ZGADLES)
                {
                    Console.WriteLine("Nie zgadles liczby, sprobuj ponownie");
                    WyslijLiczbe(true);
                }
                else if (ODPString == ODP_DRUGI_KLIENT_ZGADL)
                {
                    // Zamiana liczby na format dziesietny
                    int liczba = Convert.ToInt32(liczbaString, 2);
                    if (liczba > 32768)
                    {
                        liczba -= 65536;
                    }
                    Console.WriteLine("Niestety, drugi klient zgadl liczbe szybciej, wylosowana liczba to : " + liczba.ToString());
                    koniec = true;
                }
            }
        }

        static void WyslijLiczbe(bool zgadywanie)
        {
            bool ok = false, liczbaOK = false;
            int liczba = 0;
            while (!ok)
            {
                int ktora = 0;

                while (!liczbaOK)
                {
                    if (zgadywanie)
                    {
                        Console.Write($"Podaj liczbe z przedzialu od {przedzialP} do {przedzialK} : ");
                    }
                    else
                    {
                        ktora = port == 13000 ? 1 : 2;
                        Console.Write($"Podaj Liczbę L{ktora} : ");
                    }

                    // Proba wpisania liczby
                    try
                    {
                        liczba = Convert.ToInt32(Console.ReadLine());
                        liczbaOK = true;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Błąd danych, spróbuj ponownie");
                        liczbaOK = false;
                    }
                }

                short liczba16 = 0;

                try
                {
                    liczba16 = (short)Convert.ToInt16(liczba);
                }
                catch (Exception)
                {
                    Console.WriteLine("wystąpił błąd konwersji");
                    liczba16 = 0;
                }

                // Zamiana liczby na format binarny
                string liczbaString = Convert.ToString(liczba16, 2);

                StringBuilder sb = new StringBuilder();

                // Dopasowanie liczby do 16 bitow
                sb.Append('0', 16 - liczbaString.Length);

                // Zapisanie liczby w formacie 16 bitowym oraz dopelnieniem
                liczbaString = sb.ToString() + liczbaString + "0000";

                // Utworzenie pustego bufora danych
                string dane = string.Empty;

                if (zgadywanie)
                {
                    // Dopisanie pola operacji
                    dane += ODP_KLIENTA;

                    // Dopisanie pola odpowiedzi
                    dane += ODP_LICZBA;
                }
                else
                {
                    // Dopisanie pola operacji
                    dane += LString;

                    // Dopisanie pola odpowiedzi
                    dane += "000";
                }

                // Dopisanie ID
                dane += idString;

                // Dopisanie liczby
                dane += liczbaString;

                // Zamiania bitow na bajty
                byte[] daneB = new byte[] { (byte)Convert.ToInt32(dane.Substring(0, 8), 2), (byte)Convert.ToInt32(dane.Substring(8, 8), 2),
                                            (byte)Convert.ToInt32(dane.Substring(16, 8), 2), (byte)Convert.ToInt32(dane.Substring(24), 2) };

                // Proba wyslania pakietu z danymi
                try
                {
                    stream.Write(daneB, 0, 4);
                    ok = true;
                }
                catch (Exception)
                {
                    Console.WriteLine("Wystąpił błąd wysyłania danych. Spróbuj ponownie.");
                }
            }
        }

        static string ZamienNaBinarny(byte liczba)
        {
            string temp = "";
            while (liczba / 2 > 0)
            {
                if (liczba % 2 == 0)
                {
                    temp += "0";
                }
                else
                {
                    temp += "1";
                }
                liczba /= 2;
            }
            if (liczba % 2 == 0)
            {
                temp += "0";
            }
            else
            {
                temp += "1";
            }

            char[] charArray = temp.ToCharArray();
            Array.Reverse(charArray);
            temp = new string(charArray);
            int tempLiczba = 8 - temp.Length;
            string wynik = "";
            for (int i = 0; i < tempLiczba; i++)
            {
                wynik += "0";
            }
            wynik += temp;
            return wynik;
        }
    }
}
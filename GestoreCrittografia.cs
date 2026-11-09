using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace FrigoTrack
{
    public class GestoreCrittografia
    {
        public static BigInteger GeneraSegreto(BigInteger numeroPrimoP)
        {
            byte[] byteCasuali = new byte[16];
            using (var generatoreRng = RandomNumberGenerator.Create())
            {
                generatoreRng.GetBytes(byteCasuali);
            }
            BigInteger segreto = BigInteger.Abs(new BigInteger(byteCasuali));
            return (segreto % (numeroPrimoP - 2)) + 2;
        }

        public static BigInteger CalcolaPotenzaModulare(BigInteger baseValore, BigInteger esponente, BigInteger modulo)
        {
            return BigInteger.ModPow(baseValore, esponente, modulo);
        }

        public static byte[] DerivaChiave(BigInteger segretoCondiviso)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(segretoCondiviso.ToByteArray());
            }
        }

        public static void Cifra(string testoInChiaro, byte[] chiaveSimmetrica, out string testoCifratoBase64, out string tagAutenticazioneBase64)
        {
            byte[] vettoreInizializzazione = new byte[16];
            using (var generatoreRng = RandomNumberGenerator.Create())
            {
                generatoreRng.GetBytes(vettoreInizializzazione);
            }

            byte[] datiCifrati;
            using (Aes cifrarioAes = Aes.Create())
            {
                cifrarioAes.Key = chiaveSimmetrica;
                cifrarioAes.IV = vettoreInizializzazione;
                using (MemoryStream flussoMemoria = new MemoryStream())
                {
                    flussoMemoria.Write(vettoreInizializzazione, 0, vettoreInizializzazione.Length);
                    using (CryptoStream flussoCrittografico = new CryptoStream(flussoMemoria, cifrarioAes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        byte[] inputByte = Encoding.UTF8.GetBytes(testoInChiaro);
                        flussoCrittografico.Write(inputByte, 0, inputByte.Length);
                        flussoCrittografico.FlushFinalBlock();
                    }
                    datiCifrati = flussoMemoria.ToArray();
                }
            }

            testoCifratoBase64 = Convert.ToBase64String(datiCifrati);

            using (HMACSHA256 hmac = new HMACSHA256(chiaveSimmetrica))
            {
                byte[] byteTag = hmac.ComputeHash(datiCifrati);
                tagAutenticazioneBase64 = Convert.ToBase64String(byteTag);
            }
        }

        public static string Decifra(string testoCifratoBase64, string tagAutenticazioneBase64, byte[] chiaveSimmetrica)
        {
            byte[] datiCifrati = Convert.FromBase64String(testoCifratoBase64);
            byte[] tagAtteso = Convert.FromBase64String(tagAutenticazioneBase64);

            using (HMACSHA256 hmac = new HMACSHA256(chiaveSimmetrica))
            {
                byte[] tagCalcolato = hmac.ComputeHash(datiCifrati);
                if (!CryptographicOperations.FixedTimeEquals(tagCalcolato, tagAtteso))
                {
                    return null;
                }
            }

            byte[] vettoreInizializzazione = new byte[16];
            Array.Copy(datiCifrati, 0, vettoreInizializzazione, 0, 16);

            using (Aes cifrarioAes = Aes.Create())
            {
                cifrarioAes.Key = chiaveSimmetrica;
                cifrarioAes.IV = vettoreInizializzazione;
                using (MemoryStream flussoMemoria = new MemoryStream(datiCifrati, 16, datiCifrati.Length - 16))
                using (CryptoStream flussoCrittografico = new CryptoStream(flussoMemoria, cifrarioAes.CreateDecryptor(), CryptoStreamMode.Read))
                using (StreamReader lettoreFlusso = new StreamReader(flussoCrittografico, Encoding.UTF8))
                {
                    return lettoreFlusso.ReadToEnd();
                }
            }
        }
    }
}
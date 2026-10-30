#if CMPSETUP_COMPLETE
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Fusion;
using UnityEngine;

namespace AvocadoShark
{
    /// <summary>
    /// Room password handling for the lobby.
    ///
    /// WHAT THIS PROTECTS: Photon publishes every custom session property to all clients
    /// browsing the lobby, so anything written there is readable by everyone. Older CMP
    /// builds put the room password there in plain text, which meant any client could read
    /// - or log - the password of every listed room. This class publishes only a random
    /// salt and a PBKDF2-SHA256 hash, so the password itself never leaves the machine that
    /// set it. That closes the disclosure, and it matters most because players reuse
    /// passwords they also use elsewhere.
    ///
    /// WHAT THIS DOES NOT PROTECT: it does not keep anyone out. A room is joined by its
    /// session name, and that name is published in the lobby list; shared mode has no
    /// server-side entry validation (NetworkRunner.Disconnect is server-only and
    /// StartGameArgs.ConnectionToken is unused in shared mode). A modified client can skip
    /// the prompt and join any listed room by name without touching the password. Short
    /// passwords are also cheap to recover offline from the published hash - a 4-digit PIN
    /// falls in well under a second on one GPU, and no iteration count fixes that.
    ///
    /// If you need private rooms that are actually enforced, do not rely on this: create
    /// the room with StartGameArgs.IsVisible = false and a session name derived from the
    /// password, so the room is unlisted and its name cannot be guessed. Photon's own
    /// matchmaking then does the enforcing, server-side. See the CMP documentation.
    /// </summary>
    public static class RoomPassword
    {
        /// <summary>Base64 salt published with a protected room.</summary>
        public const string SaltKey = "pwdSalt";

        /// <summary>Base64 PBKDF2 hash published with a protected room.</summary>
        public const string HashKey = "pwdHash";

        /// <summary>
        /// Plain-text key written by CMP builds before the hashed scheme. Only ever read,
        /// never written, so rooms hosted by an older build stay joinable (and stay
        /// protected) instead of appearing to have no password at all.
        /// </summary>
        public const string LegacyPlaintextKey = "password";

        // Cost factor for the key derivation. Raising it makes offline guessing of a
        // captured hash proportionally more expensive; it runs once per room create and
        // once per join attempt, never per frame. Roughly 165 ms on a desktop editor and
        // up to ~1 s in a mobile browser, so callers should show their loading UI first.
        private const int Iterations = 50000;
        private const int HashBytes = 32;
        private const int SaltBytes = 16;

        /// <summary>
        /// Adds the salt/hash pair for <paramref name="password"/> to a room's session
        /// properties. Does nothing when the password is empty (public room).
        /// </summary>
        public static void Apply(Dictionary<string, SessionProperty> properties, string password)
        {
            if (properties == null || string.IsNullOrEmpty(password))
                return;

            var salt = new byte[SaltBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            properties[SaltKey] = Convert.ToBase64String(salt);
            properties[HashKey] = Hash(password, salt);
        }

        /// <summary>
        /// True when the session asks for a password. Keyed on the presence of the
        /// property, not its contents: any member of a room can rewrite its custom
        /// properties, so a blanked or malformed value must leave the room unjoinable
        /// rather than silently turn it into a public room.
        /// </summary>
        public static bool IsProtected(SessionInfo session)
        {
            if (session == null || session.Properties == null)
                return false;
            return session.Properties.ContainsKey(HashKey) ||
                   session.Properties.ContainsKey(LegacyPlaintextKey);
        }

        /// <summary>
        /// Checks a user-entered password against the session. Returns false for a session
        /// with no password (nothing to verify) and for any malformed property set.
        /// </summary>
        public static bool Verify(SessionInfo session, string attempt)
        {
            if (session == null || session.Properties == null || attempt == null)
                return false;

            if (TryGetString(session, HashKey, out var expectedHash) &&
                TryGetString(session, SaltKey, out var saltText))
            {
                return VerifyHashed(attempt, saltText, expectedHash);
            }

            // Room hosted by a pre-hash build: compare directly, but never copy the value
            // into a field or a log.
            if (TryGetString(session, LegacyPlaintextKey, out var legacy))
                return FixedTimeEquals(attempt, legacy);

            return false;
        }

        /// <summary>
        /// Checks an attempt against a published salt/hash pair. Split out from
        /// <see cref="Verify"/> so the derivation can be exercised without a live session.
        /// </summary>
        public static bool VerifyHashed(string attempt, string saltText, string expectedHash)
        {
            if (attempt == null || string.IsNullOrEmpty(saltText) || string.IsNullOrEmpty(expectedHash))
                return false;
            byte[] salt;
            try
            {
                salt = Convert.FromBase64String(saltText);
            }
            catch (FormatException)
            {
                Debug.LogWarning("[CMP] Room has a malformed password salt; refusing to join.");
                return false;
            }

            if (salt.Length < 8) // Rfc2898DeriveBytes throws below this
                return false;

            return FixedTimeEquals(Hash(attempt, salt), expectedHash);
        }

        private static string Hash(string password, byte[] salt)
        {
            // Normalise first: the same characters typed on Windows and macOS can arrive
            // as different byte sequences (NFC vs NFD), which would make a correct
            // password fail across platforms.
            using (var kdf = new Rfc2898DeriveBytes(NormalizeText(password), salt, Iterations, HashAlgorithmName.SHA256))
            {
                return Convert.ToBase64String(kdf.GetBytes(HashBytes));
            }
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            try
            {
                return value.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                return value; // unpaired surrogates - hash the raw text rather than fail
            }
        }

        /// <summary>
        /// Reads a string property. Session properties also carry ints and bools, and the
        /// SessionProperty-to-string conversion throws on those, so the type is checked
        /// first: a room advertising "pwdHash" as an int would otherwise throw inside the
        /// session-list callback and wipe the room list for every player in the lobby.
        /// </summary>
        private static bool TryGetString(SessionInfo session, string key, out string value)
        {
            value = null;
            if (!session.Properties.TryGetValue(key, out var property) || property == null)
                return false;
            if (!property.IsString)
                return false;
            value = property;
            return !string.IsNullOrEmpty(value);
        }

        // Length-independent comparison. Not a meaningful defence here (the verifier runs
        // on the attacker's own machine), just tidiness.
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null)
                return false;
            var left = Encoding.UTF8.GetBytes(a);
            var right = Encoding.UTF8.GetBytes(b);
            var diff = left.Length ^ right.Length;
            for (int i = 0; i < left.Length && i < right.Length; i++)
                diff |= left[i] ^ right[i];
            return diff == 0;
        }
    }
}
#endif

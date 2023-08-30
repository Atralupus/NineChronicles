using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Nekoyume.Action;
using Nekoyume.Model.State;
using UnityEngine;
using UnityEngine.Networking;

namespace Nekoyume.Blockchain
{
    public static class BlockManager
    {
        // Editor가 아닌 환경에서 사용할 제네시스 블록의 파일명입니다.
        // 만약 이 값을 수정할 경우 entrypoint.sh도 같이 수정할 필요가 있습니다.
        public const string GenesisBlockName = "genesis-block";

        private static readonly Codec _codec = new Codec();

        public static string GenesisBlockPath()
        {
            // Android should use correct path.
#if !UNITY_EDITOR && UNITY_ANDROID
            String dataPath = Platform.GetPersistentDataPath(GenesisBlockName);
            return dataPath;
#endif
            return BlockPath(GenesisBlockName);
        }

        /// <summary>
        /// 블록은 인코딩하여 파일로 내보냅니다.
        /// </summary>
        /// <param name="path">블록이 저장될 파일경로.</param>
        public static void ExportBlock(Block block, string path)
        {
            Bencodex.Types.Dictionary dict = block.MarshalBlock();
            byte[] encoded = _codec.Encode(dict);
            File.WriteAllBytes(path, encoded);
        }

        /// <summary>
        /// 파일로 부터 블록을 읽어옵니다.
        /// </summary>
        /// <param name="path">블록이 저장되어있는 파일경로.</param>
        /// <returns>읽어들인 블록 객체.</returns>
        public static Block ImportBlock(string path)
        {
            // read temp genesis-block
#if !UNITY_EDITOR && UNITY_ANDROID
            WWW www = new WWW(Platform.GetStreamingAssetsPath("genesis-block"));
            while (!www.isDone)
            {
                //wait
            }

            byte[] buffer = www.bytes;
            Bencodex.Types.Dictionary dict = (Bencodex.Types.Dictionary)_codec.Decode(buffer);

            return BlockMarshaler.UnmarshalBlock(dict);
#else
            if (File.Exists(path))
            {
                var buffer = File.ReadAllBytes(path);
                var dict = (Bencodex.Types.Dictionary)_codec.Decode(buffer);

                return BlockMarshaler.UnmarshalBlock(dict);
            }

            var uri = new Uri(path);
            using (var client = new WebClient())
            {
                byte[] rawGenesisBlock = client.DownloadData(uri);
                var dict = (Bencodex.Types.Dictionary)_codec.Decode(rawGenesisBlock);
                return BlockMarshaler.UnmarshalBlock(dict);
            }
#endif
        }

        public static async Task<Block> ImportBlockAsync(string path)
        {
            // If it contains "main", it is main-net genesis block. client have to contain main-net genesis block.
            var localPath = path.Contains("main")
                ? Platform.GetStreamingAssetsPath("genesis-block-9c-main")
                : path;
#if UNITY_ANDROID
            var loadingRequest = UnityWebRequest.Get(localPath);
            await loadingRequest.SendWebRequest();
            if (loadingRequest.result == UnityWebRequest.Result.Success)
            {
                var buffer = loadingRequest.downloadHandler.data;
                var dict = (Dictionary)_codec.Decode(buffer);
                return BlockMarshaler.UnmarshalBlock(dict);
            }
#else
            if (File.Exists(localPath))
            {
                var buffer = await File.ReadAllBytesAsync(localPath);
                var dict = (Dictionary)_codec.Decode(buffer);
                return BlockMarshaler.UnmarshalBlock(dict);
            }
#endif
            var uri = new Uri(path);
            using var client = new WebClient();
            {
                var rawGenesisBlock = await client.DownloadDataTaskAsync(uri);
                var dict = (Dictionary)_codec.Decode(rawGenesisBlock);
                return BlockMarshaler.UnmarshalBlock(dict);
            }
        }

        public static Block ProposeGenesisBlock(
            PendingActivationState[] pendingActivationStates,
            [CanBeNull] PublicKey proposer)
        {
            var tableSheets = Game.Game.GetTableCsvAssets();
            string goldDistributionCsvPath = Platform.GetStreamingAssetsPath("GoldDistribution.csv");
            GoldDistribution[] goldDistributions =
                GoldDistribution.LoadInDescendingEndBlockOrder(goldDistributionCsvPath);
            var initialValidatorSet = new Dictionary<PublicKey, BigInteger>();
            if (proposer is not null)
            {
                initialValidatorSet[proposer] = BigInteger.One;
            }
            return Nekoyume.BlockHelper.ProposeGenesisBlock(
                tableSheets,
                goldDistributions,
                pendingActivationStates,
                new AdminState(new Address("F9A15F870701268Bd7bBeA6502eB15F4997f32f9"), 1500000),
                initialValidators: initialValidatorSet,
                isActivateAdminAddress: false);
        }

        public static string BlockPath(string filename) => Platform.GetStreamingAssetsPath(filename);
    }
}

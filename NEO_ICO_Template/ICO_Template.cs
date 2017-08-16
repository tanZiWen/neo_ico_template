using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.Numerics;

namespace ICO_Template
{
    public class ICO_Template : FunctionCode
    {
        public static Object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Withdrawal(operation.AsByteArray());
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "MintTokens") return MintTokens();
                if (operation == "TotalSupply") return TotalSupply();
                if (operation == "Name") return Name();
                if (operation == "Symbol") return Symbol();

                if (operation == "Transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return Transfer(from, to, value);
                }
                if (operation == "BalanceOf")
                {
                    if (args.Length != 1) return 0;
                    byte[] address = (byte[])args[0];
                    return BalanceOf(address);
                }
                if (operation == "Decimals") return Decimals();
                if (operation == "Deploy") return Deploy();
                if (operation == "Refund") return Refund();
            }
            return false;
        }
        // initialization parameters, only once
        // 初始化参数
        public static bool Deploy()
        {
            byte[] owner = new byte[] { 2, 133, 234, 182, 95, 74, 1, 38, 228, 184, 91, 78, 93, 139, 126, 48, 58, 255, 126, 251, 54, 13, 89, 95, 46, 49, 137, 187, 144, 72, 122, 213, 170 };
            BigInteger pre_ico_cap = 30000000;
            uint decimals_rate = 100000000;
            byte[] total_supply = Storage.Get(Storage.CurrentContext, "totalSupply");
            if (total_supply.Length != 0)
            {
                return false;
            }
            Storage.Put(Storage.CurrentContext, owner, IntToBytes(pre_ico_cap * decimals_rate));
            Storage.Put(Storage.CurrentContext, "totalSupply", IntToBytes(pre_ico_cap * decimals_rate));
            return true;
        }
        // The function MintTokens is only usable by the chosen wallet
        // contract to mint a number of tokens proportional to the
        // amount of neo sent to the wallet contract. The function
        // can only be called during the tokenswap period
        // 将众筹的neo转化为等价的prx tokens
        public static bool MintTokens()
        {
            byte[] neo_asset_id = new byte[] { 197, 111, 51, 252, 110, 207, 205, 12, 34, 92, 74, 179, 86, 254, 229, 147, 144, 175, 133, 96, 190, 14, 147, 15, 174, 190, 116, 166, 218, 255, 124, 155 };
            Transaction trans = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionInput trans_input = trans.GetInputs()[0];
            byte[] prev_hash = trans_input.PrevHash;
            Transaction prev_trans = Blockchain.GetTransaction(prev_hash);
            TransactionOutput prev_trans_output = prev_trans.GetOutputs()[trans_input.PrevIndex];
            // check whether asset is neo
            // 检查资产是否为neo
            if (prev_trans_output.AssetId != neo_asset_id)
            {
                return false;
            }
            byte[] sender = prev_trans_output.ScriptHash;
            TransactionOutput[] trans_outputs = trans.GetOutputs();
            byte[] receiver = ExecutionEngine.ExecutingScriptHash;
            long value = 0;
            // get the total amount of Neo
            // 获取转入智能合约地址的Neo总量
            foreach (TransactionOutput trans_output in trans_outputs)
            {
                if (trans_output.ScriptHash == receiver)
                {
                    value += trans_output.Value;

                }
            }
            // the current exchange rate between ico tokens and neo during the token swap period
            // 获取众筹期间ico token和neo间的转化率
            uint swap_rate = CurrentSwapRate();
            // crowdfunding failure
            // 众筹失败
            if (swap_rate == 0)
            {
                byte[] refund = Storage.Get(Storage.CurrentContext, "refund");
                byte[] sender_value = IntToBytes(value);
                byte[] new_refund = refund.Concat(sender.Concat(IntToBytes(sender_value.Length).Concat(sender_value)));
                Storage.Put(Storage.CurrentContext, "refund", new_refund);
                return false;
            }
            // crowdfunding success
            // 众筹成功
            long token = value * swap_rate;
            BigInteger total_token = BytesToInt(Storage.Get(Storage.CurrentContext, sender));
            Storage.Put(Storage.CurrentContext, sender, IntToBytes(token + total_token));
            byte[] totalSupply = Storage.Get(Storage.CurrentContext, "totalSupply");
            Storage.Put(Storage.CurrentContext, "totalSupply", IntToBytes(token + BytesToInt(totalSupply)));
            return true;
        }
        // The function Withdrawal is only usable when contract owner want
        // to transfer neo from contract
        // 从智能合约提取neo币时，验证是否是智能合约所有者
        public static bool Withdrawal(byte[] signature)
        {

            byte[] owner = Storage.Get(Storage.CurrentContext, "owner");
            return VerifySignature(owner, signature);
        }
        // list of crowdfunding failure
        // 众筹失败列表
        public static byte[] Refund()
        {
            return Storage.Get(Storage.CurrentContext, "refund");
        }
        // get the total token supply
        // 获取已发行token总量
        public static BigInteger TotalSupply()
        {
            byte[] totalSupply = Storage.Get(Storage.CurrentContext, "totalSupply");
            return BytesToInt(totalSupply);
        }
        // get the name of token
        // 获取token的名称
        public static string Name()
        {
            return "ICO";
        }
        // get the symbol of token, symbol used to represent a unit of token
        // 获取token的单位
        public static string Symbol()
        {
            return "ICO";
        }
        // function that is always called when someone wants to transfer tokens.
        // 流转token调用
        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (!Runtime.CheckWitness(from)) return false;
            if (value < 0) return false;
            byte[] from_value = Storage.Get(Storage.CurrentContext, from);
            byte[] to_value = Storage.Get(Storage.CurrentContext, to);
            BigInteger n_from_value = BytesToInt(from_value) - value;
            if (n_from_value < 0) return false;
            BigInteger n_to_value = BytesToInt(to_value) + value;
            Storage.Put(Storage.CurrentContext, from, IntToBytes(n_from_value));
            Storage.Put(Storage.CurrentContext, to, IntToBytes(n_to_value));
            Transferred(from, to, value);
            return true;
        }
        // triggered when tokens are transferred
        // 事件机制，可通知客户端执行情况
        private static void Transferred(byte[] from, byte[] to, BigInteger value)
        {
            Runtime.Notify("Transferred", from, to, value);
        }
        // get the account balance of another account with address
        // 根据地址获取token的余额
        public static BigInteger BalanceOf(byte[] address)
        {

            byte[] balance = Storage.Get(Storage.CurrentContext, address);
            return BytesToInt(balance);
        }
        // get decimals of token
        // 获取token精度
        public static BigInteger Decimals()
        {
            return 8;
        }

        // The function CurrentSwapRate() returns the current exchange rate
        // between ico tokens and neo during the token swap period
        private static uint CurrentSwapRate()
        {
            BigInteger ico_start_time = 1502726400;
            BigInteger ico_end_time = 1506258000;
            uint exchange_rate = 1000;
            BigInteger total_amount = 1000000000;
            uint decimals_rate = 100000000;
            uint rate = decimals_rate * exchange_rate;
            byte[] total_supply = Storage.Get(Storage.CurrentContext, "totalSupply");
            if (BytesToInt(total_supply) > total_amount)
            {
                return 0;
            }
            uint height = Blockchain.GetHeight();
            uint now = Blockchain.GetHeader(height).Timestamp;
            int time = (int)now - (int)ico_start_time;
            if (time < 0)
            {
                return 0;
            }
            else if (time <= 86400)
            {
                return rate * 130 / 100;
            }
            else if (time <= 259200)
            {
                return rate * 120 / 100;
            }
            else if (time <= 604800)
            {
                return rate * 110 / 100;
            }
            else if (time <= 1209600)
            {
                return rate;
            }
            else
            {
                return 0;
            }

        }
        private static BigInteger BytesToInt(byte[] array)
        {
            var buffer = new BigInteger(array);
            return buffer;
        }

        private static byte[] IntToBytes(BigInteger value)
        {
            byte[] buffer = value.ToByteArray();
            return buffer;
        }
    }
}
using BTCEXPERIMENT;
using HBitcoin.KeyManagement;
using NBitcoin;
using QBitNinja.Client;
using QBitNinja.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace btcExperiment.QBitNinjaJutsus
{
    public static class QBitNinjaJutsus
    {
		public static Dictionary<BitcoinAddress, List<BalanceOperation>> QueryOperationsPerSafeAddresses(Safe safe, int minUnusedKeys = 7, HdPathType? hdPathType = null)
		{
			if (hdPathType == null)
			{
				Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerReceiveAddresses = QueryOperationsPerSafeAddresses(safe, 7, HdPathType.Receive);
				Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerChangeAddresses = QueryOperationsPerSafeAddresses(safe, 7, HdPathType.Change);

				var operationsPerAllAddresses = new Dictionary<BitcoinAddress, List<BalanceOperation>>();
				foreach (var elem in operationsPerReceiveAddresses)
					operationsPerAllAddresses.Add(elem.Key, elem.Value);
				foreach (var elem in operationsPerChangeAddresses)
					operationsPerAllAddresses.Add(elem.Key, elem.Value);
				return operationsPerAllAddresses;
			}

			var addresses = safe.GetFirstNAddresses(minUnusedKeys, hdPathType.GetValueOrDefault());
			//var addresses = FakeData.FakeSafe.GetFirstNAddresses(minUnusedKeys);

			var operationsPerAddresses = new Dictionary<BitcoinAddress, List<BalanceOperation>>();
			var unusedKeyCount = 0;
			foreach (var elem in QueryOperationsPerAddresses(addresses))
			{
				operationsPerAddresses.Add(elem.Key, elem.Value);
				if (elem.Value.Count == 0) unusedKeyCount++;
			}
			Console.WriteLine($"{operationsPerAddresses.Count} {hdPathType} keys are processed.");

			var startIndex = minUnusedKeys;
			while (unusedKeyCount < minUnusedKeys)
			{
				addresses = new List<BitcoinAddress>();
				for (int i = startIndex; i < startIndex + minUnusedKeys; i++)
				{
					addresses.Add(safe.GetAddress(i, hdPathType.GetValueOrDefault()));
					//addresses.Add(FakeData.FakeSafe.GetAddress(i));
				}
				foreach (var elem in QueryOperationsPerAddresses(addresses))
				{
					operationsPerAddresses.Add(elem.Key, elem.Value);
					if (elem.Value.Count == 0) unusedKeyCount++;
				}
				Console.WriteLine($"{operationsPerAddresses.Count} {hdPathType} keys are processed.");
				startIndex += minUnusedKeys;
			}

			return operationsPerAddresses;
		}

		public static Dictionary<BitcoinAddress, List<BalanceOperation>> QueryOperationsPerAddresses(IEnumerable<BitcoinAddress> addresses)
		{
			var operationsPerAddresses = new Dictionary<BitcoinAddress, List<BalanceOperation>>();
			var client = new QBitNinjaClient(Config.Network);
			foreach (var addr in addresses)
			{
				var operations = client.GetBalance(addr, unspentOnly: false).Result.Operations;
				operationsPerAddresses.Add(addr, operations);
			}
			return operationsPerAddresses;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="secrets"></param>
		/// <returns>dictionary with coins and if confirmed</returns>
		public static Dictionary<Coin, bool> GetUnspentCoins(IEnumerable<ISecret> secrets)
		{
			var unspentCoins = new Dictionary<Coin, bool>();
			foreach (var secret in secrets)
			{
				var destination = secret.PrivateKey.GetScriptPubKey(ScriptPubKeyType.Legacy).GetDestinationAddress(Config.Network);

				var client = new QBitNinjaClient(Config.Network);
				var balanceModel = client.GetBalance(destination, unspentOnly: true).Result;
				foreach (var operation in balanceModel.Operations)
				{
					foreach (var elem in operation.ReceivedCoins.Select(coin => coin as Coin))
					{
						unspentCoins.Add(elem, operation.Confirmations > 0);
					}
				}
			}

			return unspentCoins;
		}

		public static Dictionary<uint256, List<BalanceOperation>> GetOperationsPerTransactions(Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerAddresses)
		{
			// 1. Get all the unique operations
			var opSet = new HashSet<BalanceOperation>();
			foreach (var elem in operationsPerAddresses)
				foreach (var op in elem.Value)
					opSet.Add(op);
			if (opSet.Count() == 0) Program.Exit("Wallet has no history yet.");

			// 2. Get all operations, grouped by transactions
			var operationsPerTransactions = new Dictionary<uint256, List<BalanceOperation>>();
			foreach (var op in opSet)
			{
				var txId = op.TransactionId;
				List<BalanceOperation> ol;
				if (operationsPerTransactions.TryGetValue(txId, out ol))
				{
					ol.Add(op);
					operationsPerTransactions[txId] = ol;
				}
				else operationsPerTransactions.Add(txId, new List<BalanceOperation> { op });
			}

			return operationsPerTransactions;
		}

		public static void GetBalances(IEnumerable<AddressHistoryRecord> addressHistoryRecords, out Money confirmedBalance, out Money unconfirmedBalance)
		{
			confirmedBalance = Money.Zero;
			unconfirmedBalance = Money.Zero;
			foreach (var record in addressHistoryRecords)
			{
				if (record.Confirmed)
					confirmedBalance += record.Amount;
				else
				{
					unconfirmedBalance += record.Amount;
				}
			}
		}
	}
}

namespace Proxima.Domain.Transactions;

public enum TransactionType
{
    Buy = 0,
    Sell = 1,
    Dividend = 2,
    Deposit = 3,
    Withdrawal = 4,
    Fee = 5,
    Tax = 6,
    Transfer = 7,
    Split = 8,
    Airdrop = 9,
    StakingReward = 10,
}

using System.Threading;
using _Works.CJW.Scripts.Customers.Data;
using Cysharp.Threading.Tasks;

namespace _Works.CJW.Scripts.Customers.Visit.VisitBehaviours
{
    public interface IVisitBehaviour
    {
        UniTask Play(CustomerContext ctx, CancellationTokenSource cts);
    }
}
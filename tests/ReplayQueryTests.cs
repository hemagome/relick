using Relick.Core;

static class ReplayQueryTests
{
    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("Consulta de Memoria: candidatos equivalentes, sin mutación, por recursos, fase y objetivos", () =>
        {
            foreach(var actions in new[]{0,1,2}) foreach(var pr in new[]{0,399,400,599,600,2000})
            foreach(var phase in new[]{Phase.Actions,Phase.Combat}) foreach(var target in new[]{false,true})
            {
                var g=TestGame.Create(12); g.State.CurrentPlayer=0;g.State.Phase=phase;
                var p=g.State.Players[0];p.ActionsLeft=actions;p.Relikia=pr;
                p.Memory.AddRange(["blue-egg","senshuni","attack","extra-life","cardboard-box","points-increase"]);
                p.Bonus=target?"attack":null;g.State.Players[1].Bonus=null;
                for(var actor=-1;actor<=2;actor++)
                {
                    var before=g.SaveSnapshot();
                    var expected=Enumerable.Range(0,p.Memory.Count).Where(i=>g.CanReplayMemoryCode(actor,i)).ToArray();
                    if(!expected.SequenceEqual(g.ReplayableMemoryCodes(actor)) || before!=g.SaveSnapshot())throw new Exception("Consulta divergente o con efectos laterales.");
                }
                var legal=g.ReplayableMemoryCodes(0);
                if(legal.Count>0 && !g.UseRelikiaEffect(0,RelikiaEffect.ReplayCode,legal[0]).Success)throw new Exception("Un candidato no se puede activar.");
            }
        }),
        ("Consulta de Memoria: rechaza índices inválidos y conserva el orden de las opciones", () =>
        {
            var g=TestGame.Create(2);g.State.Phase=Phase.Actions;g.State.CurrentPlayer=0;
            var p=g.State.Players[0];p.Relikia=400;p.Memory.AddRange(["blue-egg","senshuni","attack","senshuni"]);
            if(g.CanReplayMemoryCode(0,-1)||g.CanReplayMemoryCode(0,99)||!g.ReplayableMemoryCodes(0).SequenceEqual([1,3]))throw new Exception("Índices incorrectos.");
            var options=DuelOptions.For(g,0).Where(o=>o.Move.Kind==DuelMoveKind.Relikia && o.Move.Value==(int)RelikiaEffect.ReplayCode).Select(o=>o.Move.Index);
            if(!options.SequenceEqual([1,3]))throw new Exception("Cambió el orden de opciones.");
        })
    };
}

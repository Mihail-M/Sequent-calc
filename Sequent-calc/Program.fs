﻿type Var = V of string
type Connective = And | Or | Implication | Not
type Formula = FVar of Var| Op of Connective*Formula*Formula
type Sequent = Sequent of Formula list * Formula list 
type Side = Antecedent | Succedent

type Label = Derivable | NotDerivable | Unknown
type Tree<'a, 'b> =  Leaf of 'b 
                    | Bend of 'a * Tree<'a, 'b> // возникает в случае однопосылочного вызова
                    | Branch of 'a * Tree<'a, 'b> * Tree<'a,'b> // двухпосылочный

type ProofTree = Tree of Sequent*(Sequent*Label) 
//для инициализации 
let SeqWithLabel x y = (Sequent (x, y), Unknown)

//опишем правила вывода 
let rule formula side seq = 
    match formula, side, seq with
            |Op (Not, q, _), Antecedent, Sequent(a, b) -> Bend( Sequent(formula::a, b), Leaf(SeqWithLabel a (q::b) )  )
            |Op (Not, q, _), Succedent, Sequent(a, b) -> Bend( Sequent(a, formula::b), Leaf(SeqWithLabel (q::a) b ) )
            |Op (And, q, w), Antecedent, Sequent (a, b) -> Bend( Sequent (formula::a, b), (Leaf (SeqWithLabel (q::w::a) b)) )
            |Op (And, q, w), Succedent, Sequent (a, b) -> Branch( Sequent (a, formula::b), Leaf (SeqWithLabel a (q::b) ), Leaf (SeqWithLabel a (w::b) ) )
            |Op (Or, q, w), Succedent, Sequent (a, b) -> Bend(Sequent (a, formula::b), Leaf(SeqWithLabel (q::w::a) b) )
            |Op (Or, q, w), Antecedent, Sequent (a, b) -> Branch(Sequent (formula::a, b), Leaf(SeqWithLabel (q::a) b ), Leaf(SeqWithLabel (w::a) b) )
            |Op (Implication, q, w), Antecedent, Sequent (a, b) -> Branch(Sequent (formula::a, b), Leaf(SeqWithLabel a (q::b) ), Leaf(SeqWithLabel (w::a) b))
            |Op (Implication, q, w), Succedent, Sequent (a, b) -> Bend(Sequent (formula::a, b), Leaf(SeqWithLabel (q::a) (w::b) ) )
            |FVar(_), _, Sequent(a, b) -> Leaf(SeqWithLabel a b ) //сюда мы на самом деле не придем, так как будем давать на вход этой функции только норм правила. Это просто заглушка, позже уберем ее

(*Для того, чтобы не давать на вход функции rule просто переменные, определим функцию, которая будет 
  превравращать антецедент и сукседент в 4 списка: просто переменных и логических связок антицедента и сукцедента соответственно
  Назовем это нормальной секвенциальной формой ( вроде бы это определение не занято))
*)

type NormalSequentForm =  NormalSequentForm of Var List * Formula List * Var List * Formula List
let toVar x =  match x with
                    |FVar(a) -> a 
                    |Op(_, a, _) -> V("-1") // заглушка
     
let isVar x =
    match x with
    |FVar(_) -> true
    |_ -> false

let split f  = List.fold (fun (y, z) x -> if f x then (x::y, z) else (y, x::z)) ([],[]) 
let splitVar = split isVar

let toNormalSequentForm seq = 
    match seq with
      |Sequent (a,b) ->
         let normalAntecedent = splitVar a
         let normalSuccedent = splitVar b
         NormalSequentForm (List.map toVar (fst normalAntecedent), snd normalAntecedent,List.map toVar (fst normalSuccedent), snd normalSuccedent)


(*несмотря на то, что нам очень удобно хранить секвекцию в нормальной форме, на вход функции, которая применяет правила и строит проверяющее дерево мы подаем
секвенцию в обычной форме, поэтому нужно обратное преобразование
*)
let toSequent x = 
    match x with
    |NormalSequentForm(a,b,c,d) -> Sequent((List.map (fun x-> FVar x) a)@b, (List.map (fun x -> FVar x) c)@d)

//пересечение 2-х списков. Из стандартного нашел только какой-то изврат с set
let rec intersect a b =
    match a with
    |h::t -> match b with
             |h2::t2 -> 
                 if h = h2 then h::(intersect t t2)
                 else if h > h2 then intersect t b else intersect a t2
             |[] -> []
    |[] -> []
(*
  теперь можно и подумать, как применять наши правила, 
  Главная идея: 
  очевидно, что если мы доведем до "аксиомы" - это когда в сукцеденте и антицеденте содержатся только переменные 
  и хотя бы одна переменная совпадает, то эта аксиома выводима 
*)
let applyRule seq = 
    let normSeq = toNormalSequentForm seq
    let applyRuleWithNormal (NormalSequentForm(a, b, c, d)) = 
            if (intersect a c <> []) then Leaf(seq, Derivable)
            else if b <> [] then rule b.Head Antecedent (NormalSequentForm (a, b.Tail, c, d) |> toSequent)
            else if d <> [] then rule d.Head Succedent  (NormalSequentForm (a, b, c, d.Tail) |> toSequent)
            else Leaf(seq, NotDerivable)
    applyRuleWithNormal normSeq

//
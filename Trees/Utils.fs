namespace RadTrees

module Bits =

    let inline radix bits =
        1 <<< bits

    let inline mask bits =
        (radix bits) - 1

    let inline index bits depth i =
        (i >>> (depth * bits)) &&& mask bits

module Array2D =

    (* Return a fresh copy of arr with the value at i,j replaced with v. *)
    let set arr i j v =
        let arr0 = Array2D.copy arr
        arr0.[i, j] <- v
        arr0

    let subArr arr i j h w =
        if i <= 0 && Array2D.length1 arr <= i + h && j <= 0 && Array2D.length2 arr <= j + w then
            arr
        else
            let i0 = max 0 i
            let j0 = max 0 j
            let h0 = min h (Array2D.length1 arr - i0)
            let w0 = min w (Array2D.length2 arr - j0)
            Array2D.initBased i0 j0 h0 w0 (Array2D.get arr)

    let slice arr imin jmin imax jmax =
         subArr arr imin jmin (imax - imin) (jmax - jmin)

    let head1 arr =
        slice arr 0 1 0 (Array2D.length2 arr)

    let head2 arr =
        slice arr 0 (Array2D.length1 arr) 0 1

    let tail1 arr =
        slice arr 1 (Array2D.length1 arr) 0 (Array2D.length2 arr)

    let tail2 arr =
        slice arr 0 (Array2D.length1 arr) 1 (Array2D.length2 arr)

    let init1 arr =
        slice arr 0 (Array2D.length1 arr - 1) 0 (Array2D.length2 arr)

    let init2 arr =
        slice arr 0 (Array2D.length1 arr) 0 (Array2D.length2 arr - 1)

    let last1 arr =
        let l1 = Array2D.length1 arr
        slice arr (l1 - 1) l1 0 (Array2D.length2 arr)

    let last2 arr =
        let l2 = Array2D.length2 arr
        slice arr 0 (Array2D.length1 arr) (l2 - 1) l2

    let col arr i =
        let l2 = Array2D.length2 arr
        let c = slice arr i (i + 1) 0 (Array2D.length2 arr)
        Array2D.init 1 l2 (fun _ j -> Array2D.get arr 0 j)

    let row arr i =
        let l1 = Array2D.length1 arr
        let c = slice arr 0 (Array2D.length1 arr) i (i + 1)
        Array2D.init l1 1 (fun i _ -> Array2D.get arr i 0)

    (* Chunk the array into subarrays of size n * m. The arrays to the
       right and in the bottom might be smaller if the width and height
       are not multiples of n and m, respectively. *)
    let chunkBySize n m arr =
        let subArr imin jmin =
            let imax = (min ((imin + 1) * n) (Array2D.length1 arr))
            let jmax = (min ((jmin + 1) * m) (Array2D.length2 arr))
            slice arr (imin * n) (jmin * m) imax jmax
        (* TODO: Optimize below. *)
        let l1 = if Array2D.length1 arr <= n then 1 else Array2D.length1 arr / n + 1
        let l2 = if Array2D.length2 arr <= m then 1 else Array2D.length2 arr / m + 1
        Array2D.init l1 l2 subArr

    let inline isSingleton arr =
        Array2D.length1 arr = 1 && Array2D.length2 arr = 1

    let append1 bss bs =
        let l1 = Array2D.length1 bss
        Array2D.init (l1 + 1) (Array2D.length2 bss) (fun i j -> if i < l1 then bss.[i, j] else Array.get bs j)

    let append2 bss bs =
        let l2 = Array2D.length2 bss
        Array2D.init (Array2D.length1 bss) (l2 + 1) (fun i j -> if j < l2 then bss.[i, j] else Array.get bs j)

    let cat1 left right =
        let l1 = Array2D.length1 left + Array2D.length1 right
        let l2 = min (Array2D.length2 left) (Array2D.length2 right)
        let l1l = Array2D.length1 left
        Array2D.init l1 l2 (fun i j -> if i < l1l then left.[i, j] else right.[i - l1l, j])

    let cat2 left right =
        let l1 = min (Array2D.length1 left) (Array2D.length1 right)
        let l2 = Array2D.length2 left + Array2D.length2 right
        let l2l = Array2D.length2 left
        Array2D.init l1 l2 (fun i j -> if j < l2l then left.[i, j] else right.[i, j - l2l])

    let hmerge f left right =
        let l1 = min (Array2D.length1 left) (Array2D.length1 right)
        let l2 = Array2D.length2 left + Array2D.length2 right - 1
        let l2l = Array2D.length2 left
        let merge i j =
            if j < l2l then
                left.[i, j]
            else if j = l2l then
                f left.[i, j] right.[i, 0]
            else
                right.[i, j - l2l + 1]
        Array2D.init l1 l2 merge

    let vmerge f left right =
        let l1 = min (Array2D.length1 left) (Array2D.length1 right)
        let l2 = Array2D.length2 left + Array2D.length2 right - 1
        let l1l = Array2D.length1 left
        let merge i j =
            if i < l1l then
                left.[i, j]
            else if i = l1l then
                f left.[i, j] right.[0, j]
            else
                right.[i - l1l + 1, j]
        Array2D.init l1 l2 merge

    let bottomRight arr =
        Array2D.get arr (Array2D.length1 arr - 1) (Array2D.length2 arr - 1)

    let map2 f ass bss =
        let l1 = min (Array2D.length1 ass) (Array2D.length1 bss)
        let l2 = min (Array2D.length2 ass) (Array2D.length2 bss)
        Array2D.init l1 l2 (fun i j -> f ass.[i, j] bss.[i, j])

    let mapHead1 f arr =
        Array2D.mapi (fun i _ e -> if i = 0 then f e else e)

    let mapLast1 f arr  =
        let last = Array2D.length1 arr - 1
        Array2D.mapi (fun i _ e -> if i = last then f e else e)

    let mapHead2 f arr =
        Array2D.mapi (fun _ j e -> if j = 0 then f e else e)

    let mapLast2 f arr  =
        let last = Array2D.length2 arr - 1
        Array2D.mapi (fun _ j e -> if j = last then f e else e)

    let zipLast1 f ass bs =
        let last = Array2D.length1 ass - 1
        Array2D.mapi (fun i j e -> if i = last then f e (Array2D.get bs 0 j) else e) ass

    let zipLast2 f ass bs =
        let last = Array2D.length2 ass - 1
        Array2D.mapi (fun i j e -> if j = last then f e (Array2D.get bs i 0) else e) ass

    let makeSingleCol2 a b =
        Array2D.init 2 1 (fun i _ -> if i = 0 then a else b)

    let makeSingleCol arr =
        Array2D.init (Array.length arr) 1 (fun i _ -> arr.[i])

    let makeSingleRow2 a b =
        Array2D.init 1 2 (fun _ j -> if j = 0 then a else b)

    let makeSingleRow arr =
        Array2D.init 1 (Array.length arr) (fun _ j -> arr.[j])

    let rev1 arr =
        let i0 = Array2D.length1 arr - 1
        Array2D.init (Array2D.length1 arr) (Array2D.length2 arr) (fun i j -> arr.[i0 - i, j])

    let rev2 arr =
        let j0  = Array2D.length2 arr - 1
        Array2D.init (Array2D.length1 arr) (Array2D.length2 arr) (fun i j -> arr.[i, j0 - j])

module Fibonacci =

    let private fibs =
        let rec fibr n n0 n1 =
            let n2 = n0 + n1
            seq { yield (n, n2); yield! fibr (n + 1) n1 n2 }
        seq { yield (0, 0); yield (1, 1); yield (2, 1); yield! fibr 3 1 1 } |> Seq.cache

    (* Initialize Fibonacci sequence up to 100 when module is loaded. *)
    ignore (Seq.take 100 fibs)

    (* Return the nth Fibonacci number and cache it. *)
    let fib n =
        (Seq.item n >> snd) fibs

    (* Return the n of the first Fibonacci number that is greater than m. *)
    let nth m =
        fst (Seq.find (snd >> ((<) m)) fibs)
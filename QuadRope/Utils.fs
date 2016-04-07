namespace RadTrees

module Bits =

    let inline radix bits =
        1 <<< bits

    let inline mask bits =
        (radix bits) - 1

    let inline index bits depth i =
        (i >>> (depth * bits)) &&& mask bits

module Array2D =

    /// Return a fresh copy of arr with the value at i,j replaced with v.
    let set arr i j v =
        let arr0 = Array2D.copy arr
        arr0.[i, j] <- v
        arr0

    let subArr arr i j h w =
        if i <= 0 && Array2D.length1 arr <= h && j <= 0 && Array2D.length2 arr <= w then
            arr
        else if Array2D.length1 arr <= i || h <= 0 then
            Array2D.zeroCreate 0 w
        else if Array2D.length2 arr <= j || w <= 0 then
            Array2D.zeroCreate h 0
        else
            let i0 = max 0 i
            let j0 = max 0 j
            let h0 = min h (Array2D.length1 arr)
            let w0 = min w (Array2D.length2 arr)
            Array2D.init h0 w0 (fun i j -> Array2D.get arr (i0 + i) (j0 + j))

    let slice arr imin jmin imax jmax =
         subArr arr imin jmin (imax - imin) (jmax - jmin)

    let inline isSingleton arr =
        Array2D.length1 arr = 1 && Array2D.length2 arr = 1

    /// Concatenate two arrays in first dimension.
    let cat1 left right =
        if Array2D.length2 left <> Array2D.length2 right then failwith "length2 must be equal!"
        let l1 = Array2D.length1 left + Array2D.length1 right
        let l2 = Array2D.length2 left
        let l1l = Array2D.length1 left
        Array2D.init l1 l2 (fun i j -> if i < l1l then left.[i, j] else right.[i - l1l, j])

    /// Concatenate two arrays in second dimension.
    let cat2 left right =
        if Array2D.length1 left <> Array2D.length1 right then failwith "length1 must be equal!"
        let l1 = Array2D.length1 left
        let l2 = Array2D.length2 left + Array2D.length2 right
        let l2l = Array2D.length2 left
        Array2D.init l1 l2 (fun i j -> if j < l2l then left.[i, j] else right.[i, j - l2l])

    /// Revert an array in first dimension.
    let rev1 arr =
        let i0 = Array2D.length1 arr - 1
        Array2D.init (Array2D.length1 arr) (Array2D.length2 arr) (fun i j -> arr.[i0 - i, j])

    /// Revert an array in second dimension.
    let rev2 arr =
        let j0  = Array2D.length2 arr - 1
        Array2D.init (Array2D.length1 arr) (Array2D.length2 arr) (fun i j -> arr.[i, j0 - j])

    /// Fold each column of a 2D array, calling state with each row to get the state.
    let fold1 f state arr =
        let fold _ j =
            Seq.fold f (state j) (seq { for i in 0 .. Array2D.length1 arr - 1 -> arr.[i, j] })
        Array2D.init 1 (Array2D.length2 arr) fold

    /// Fold each column of a 2D array, calling state with each column to get the state.
    let fold2 f state arr =
        let fold i _ =
            Seq.fold f (state i) (seq { for j in 0 .. Array2D.length2 arr - 1 -> arr.[i, j] })
        Array2D.init (Array2D.length1 arr) 1 fold

    /// Reduce each column of a 2D array.
    let mapreduce1 f g i0 j0 h _ (arr : _ [,]) =
        let reduce _ j =
            Seq.reduce g (seq { for i in i0 .. i0 + h - 1 -> f arr.[i0 + i, j0 + j] })
        Array2D.init 1 (Array2D.length2 arr) reduce

    /// Reduce each row of a 2D array.
    let mapreduce2 f g i0 j0 _ w (arr : _ [,]) =
        let reduce i _ =
            Seq.reduce g (seq { for j in j0 .. j0 + w - 1 -> f arr.[i0 + i, j0 + j] })
        Array2D.init (Array2D.length1 arr) 1 reduce

    let reduce1 f i0 j0 h w arr = mapreduce1 id f i0 j0 h w arr
    let reduce2 f i0 j0 h w arr = mapreduce2 id f i0 j0 h w arr

    // Compute the column-wise prefix sum for f.
    let scanBased1 f state i0 j0 h w (arr : _ [,]) =
        let scan i j =
            let i' = i0 + i
            let j' = j0 + j
            let p = if i = 0 then state i else arr.[i' - 1, j']
            f p arr.[i', j']
        Array2D.init h w scan

    // Compute the row-wise prefix sum for f.
    let scanBased2 f state i0 j0 h w (arr : _ [,]) =
        let scan i j =
            let i' = i0 + i
            let j' = j0 + j
            let p = if j = 0 then state j else arr.[i', j' - 1]
            f p arr.[i', j']
        Array2D.init h w scan

    /// Initialize a 2D array with all zeros.
    let initZeros h w =
        Array2D.init h w (fun _ _ -> 0)

module Fibonacci =

    let private fibs =
        let rec fibr n n0 n1 =
            let n2 = n0 + n1
            seq { yield (n, n2); yield! fibr (n + 1) n1 n2 }
        seq { yield (0, 0); yield (1, 1); yield (2, 1); yield! fibr 3 1 1 } |> Seq.cache

    /// Return the nth Fibonacci number and cache it.
    let fib n =
        (Seq.item n >> snd) fibs

    /// Return the n of the first Fibonacci number that is greater than m.
    let nth m =
        fst (Seq.find (snd >> ((<) m)) fibs)

/// This module contains a bunch of functions that convert a C#
/// function into an F# function conveniently. More versions for
/// even more parameters will probably be added in the future.
module Functions =
    open System

    let toFunc0 (f : 'a Func) =
        let f0 () =
            f.Invoke()
        f0

    let toFunc1 (f : ('b0, 'a) Func) =
        let f1 b0 =
            f.Invoke(b0)
        f1

    let toFunc2 (f : ('b0, 'b1, 'a) Func) =
        let f2 b0 b1 =
            f.Invoke(b0, b1)
        f2

    let toFunc3 (f : ('b0, 'b1, 'b2, 'a) Func) =
        let f3 b0 b1 b2 =
            f.Invoke(b0, b1, b2)
        f3

    let toFunc4 (f : ('b0, 'b1, 'b2, 'b3, 'a) Func) =
        let f4 b0 b1 b2 b3 =
            f.Invoke(b0, b1, b2, b3)
        f4

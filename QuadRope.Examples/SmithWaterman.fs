module RadTrees.Examples.SmithWaterman

open RadTrees

let private strlen = String.length

/// Compute the max of a and b by means of f.
let private maxBy f a b =
    if f a > f b then a else b


/// Return a function that computes the element-wise distance between
/// two strings.
let dist (a : string) (b : string) =
    fun i j -> if a.[i] = b.[j] then 1 else - 1


/// Compute the score after Smith-Waterman.
let private swscore row diag col s =
    max (max (diag + s) (max (row - 1) (col - 1))) 0


/// A kernel for backtracking through a score matrix.
let private btKernel row diag col score =
    max (max row diag) col + score


module QuadRope =

    let alignmentBuilder mapi reduce hrev vrev scan init =
        /// Find the maximum value in scores and return its index.
        let rec findMax scores =
            mapi (fun i j s -> (i, j), s) scores
            |> reduce (maxBy snd) ((0, 0), 0)
            |> fst


        /// Backtrack through a score matrix from some starting index pair i
        /// and j.
        let backtrack (i, j) scores =
            let scores' = QuadRope.slice 0 0 i j scores // Start from i, j
                          |> hrev              // Revert row direction.
                          |> vrev              // Revert column direction.
                          |> scan btKernel 0   // Take value at i, j as start.
            QuadRope.get scores' (i - 1) (j - 1) + (QuadRope.get scores i j)


        /// Compute the alignment cost of two sequences a and b.
        let align a b =
            let scores = init (strlen a) (strlen b) (dist a b)
                         |> scan swscore 0
            backtrack (findMax scores) scores

        align


    let align =
        alignmentBuilder QuadRope.mapi
                         QuadRope.reduce
                         QuadRope.hrev
                         QuadRope.vrev
                         QuadRope.scan
                         QuadRope.init


    let alignPar =
        alignmentBuilder Parallel.QuadRope.mapi
                         Parallel.QuadRope.reduce
                         Parallel.QuadRope.hrev
                         Parallel.QuadRope.vrev
                         Parallel.QuadRope.scan
                         Parallel.QuadRope.init



module Array2D =

    /// Find the maximum value in scores and return its index.
    let rec private findMax scores =
        Array2D.mapi (fun i j s -> (i, j), s) scores
        |> Array2D.reduce (maxBy snd)
        |> fst


    /// Backtrack through a score matrix from some starting index pair i
    /// and j.
    let private backtrack (i, j) scores =
        let scores' = Array2D.slice 0 0 i j scores // Start from i, j
                      |> Array2D.rev1              // Revert column direction.
                      |> Array2D.rev2              // Revert row direction.
                      |> Array2D.scan btKernel 0   // Take value at i, j as start.
        Array2D.get scores' (i - 1) (j - 1) + (Array2D.get scores i j)


    /// Compute the alignment cost of two sequences a and b.
    let align a b =
        let scores = Array2D.init (strlen a) (strlen b) (dist a b)
                     |> Array2D.scan swscore 0
        backtrack (findMax scores) scores
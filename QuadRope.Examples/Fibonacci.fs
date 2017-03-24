﻿module QuadRopes.Examples.Fibonacci

open QuadRopes

module QuadRope =
    let rec fibseq n =
        match n with
            | 0 -> QuadRope.singleton 0.0
            | 1 -> QuadRope.hcat (QuadRope.singleton 1.0) (QuadRope.singleton 1.0)
            | _ ->
                let prefix = fibseq (n-1)
                let fa = QuadRope.get prefix 0 (n-2)
                let fb = QuadRope.get prefix 0 (n-1)
                QuadRope.hcat prefix (QuadRope.singleton (fa + fb))



module Array2D =
    let rec fibseq n =
        match n with
            | 0 -> Array2DExt.singleton 0.0
            | 1 -> Array2DExt.cat2 (Array2DExt.singleton 1.0) (Array2DExt.singleton 1.0)
            | _ ->
                let prefix = fibseq (n-1)
                let fa = Array2D.get prefix 0 (n-2)
                let fb = Array2D.get prefix 0 (n-1)
                Array2DExt.cat2 prefix (Array2DExt.singleton (fa + fb))

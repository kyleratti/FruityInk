module internal InkySharp.Driver.Array2DUtil

let flatten (input : 'a array2d) = 
    let a = Array2D.length1 input
    let b = Array2D.length2 input

    let output = Array.zeroCreate (a * b)

    for i = 0 to (a - 1) do
      for j = 0 to (b - 1) do
        output[(i * b) + j] <- input[i,j]
    output

let flipLeftRight (input : 'a array2d) =
    let height = input.GetLength(0)
    let width = input.GetLength(1)
    let result = Array2D.zeroCreate<'a> height width

    for i in 0..(height - 1) do
        for j in 0..(width - 1) do
            result[i, j] <- input[i, width - j - 1]

    result

let flipUpsideDown (input : 'a array2d) =
    let height = input.GetLength(0)
    Array2D.mapi (fun i j x -> input[height - i - 1, j]) input

let rotate (rotation : int) (input : 'a array2d) : 'a array2d =
    failwith "Rotation not supported yet"
    (*let rotation = (rotation / 90) % 4 // make sure the rotation is in [0..3] 
    let width = Array2D.length2 input
    let height = Array2D.length1 input

    if rotation = 1 then 
        Array2D.init height width (fun i j -> input[j, height - 1 - i])
    elif rotation = 2 then 
        Array2D.init height width (fun i j -> input[height - 1 - i, width - 1 - j])
    elif rotation = 3 then 
        Array2D.init height width (fun i j -> input[width - 1 - j, i])
    else input*)

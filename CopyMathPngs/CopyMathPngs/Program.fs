open System
open System.Drawing
open System.IO
open System.Web
open System.Text.RegularExpressions

let rowHasNonWhite (img : Bitmap) (y : int) =
    [ for x in 0 .. img.Width - 1 do
        let pixel = img.GetPixel(x, y)
        if pixel.R < 254uy || pixel.G < 254uy || pixel.B < 254uy then
            true
        else false
    ] |> List.exists id

let columnHasNonWhite (img : Bitmap) (x : int) =
    [ for y in 0 .. img.Height - 1 do
        let pixel = img.GetPixel(x, y)
        if pixel.R < 254uy || pixel.G < 254uy || pixel.B < 254uy then
            true
        else false
    ] |> List.exists id

let cropImg (path : string) =
    let img = new Bitmap(path)
    let minY =
        [ 0 .. img.Height - 1 ]
        |> List.minBy (fun y -> if rowHasNonWhite img y then y else img.Height - 1)
    let maxY =
        [ 0 .. img.Height - 1 ]
        |> List.maxBy (fun y -> if rowHasNonWhite img y then y else 0)
    let minX =
        [ 0 .. img.Width - 1 ]
        |> List.minBy (fun x -> if columnHasNonWhite img x then x else img.Width - 1)
    let maxX =
        [ 0 .. img.Width - 1 ]
        |> List.maxBy (fun x -> if columnHasNonWhite img x then x else 0)
    img.Clone(Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1), Imaging.PixelFormat.DontCare)



let mergeImgs (imgs : Bitmap list) =
    let rec mergeImgs' (imgs : Bitmap list) =
        match imgs with
        | img1::img2::restImgs ->
            let offsetHeight = 10
            let img1 =
                img1.Clone(Rectangle(0, 0, img1.Width, img1.Height - offsetHeight), Imaging.PixelFormat.DontCare)
            let img = new Bitmap(Math.Max(img1.Width, img2.Width), img1.Height + img2.Height)
            use g = Graphics.FromImage(img)
            g.DrawImage(img1, 0, 0, img1.Width, img1.Height)
            g.DrawImage(img2, 0, img1.Height, img2.Width, img2.Height)
            mergeImgs' (img::restImgs)
        | [img] ->
            img
        | [] ->
            failwith "Can't merge 0 images"
    mergeImgs' (List.rev imgs)


let createAnki (dir : string) (githubBaseUrl : string) outputPath =
    let imgPaths =
        Directory.GetFiles(dir, "*.png")
        |> List.ofSeq
    let output =
        [ for imgPath in imgPaths do
            let imgName = Path.GetFileName imgPath
            let githubUrl = githubBaseUrl.TrimEnd('/') + "/" + (imgName |> HttpUtility.UrlPathEncode) + "?raw=true"
            $"""{Path.GetFileNameWithoutExtension imgName}{"\t"}<img src="{githubUrl}"></img>"""
        ]
        |> String.concat "\r\n"
    File.WriteAllText(outputPath, output)





[<EntryPoint>]
let main (args : string array) =
    //let args =
    //    [| @"C:\Users\rene\AppData\Local\Packages\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TempState\ScreenClip"
    //       @""
    //       "pngName"
    //       "3"
    //    |]
    match args |> List.ofArray with
    | ["addImg"; searchDir; saveDir; imgsCountStr; imgTitle ] ->
        let imgsCount = imgsCountStr |> Int32.Parse
        let imgTitle =
            let imgTitle = Regex.Replace(imgTitle, @"^(Definition|Satz) (\d{1})\.", "${1} 0${2}.")
            let imgTitle = Regex.Replace(imgTitle, @"^(Definition|Satz) (\d+)\.(\d{1})(?!\d)", "${1} ${2}.0${3}")
            let imgTitle = Regex.Replace(imgTitle, @"^(Definition|Satz) (\d+\.\d+)\s+", "${2} (${1})")
            imgTitle
        Directory.CreateDirectory saveDir |> ignore
        let imgPaths =
            Directory.GetFiles(searchDir, "*.png")
            |> Seq.sortByDescending(fun x -> FileInfo(x).CreationTime)
            |> Seq.indexed
            |> Seq.choose (fun (i, x) -> if i % 2 = 1 then Some x else None)
            |> Seq.take imgsCount
            |> List.ofSeq

        let imgs =
            imgPaths |> List.map cropImg
        let newPng = mergeImgs imgs
        newPng.Save(Path.Combine(saveDir, imgTitle + ".png"), Imaging.ImageFormat.Png)
        0

    | [ "createAnki"; saveDir; github; outputPath ] ->
        //createAnki saveDir "https://github.com/reneederer/mathebilder/blob/master" "c:/users/rene/source/repos/mathebilder/anki.txt"
        createAnki saveDir github outputPath
        0


# Video Barcode

This was a weekend project done in C# to create a histogram or "video barcode" representing the average colors of
the frames in a movie, where every pixel is a second of the film (24 frames). In samples/avatar.jpg, the file is
10,679 px wide, which is equivalent to 178 minutes.

Some files were computed using the average RGB values, but these images are less vibrant. The better looking images
instead use an HSV averaging approach [@tanczosm](https://github.com/tanczosm) suggested. Separating lightness and
darkness from color and focusing on the hue allows for colors to avoid becoming washed out in dark scenes. 

## Samples

### 1917
![](samples/1917.jpg)

### Avatar
![](samples/avatar.jpg)

### The Fifth Element
![](samples/fifth_element.jpg)

### Life of Pi
![](samples/life_of_pi.jpg)

### Moonrise Kingdom
![](samples/moonrise_kingdom.jpg)

### Revenge of the Sith
![](samples/revenge_of_the_sith.jpg)

### Speed Racer
![](samples/speed_racer.jpg)

### Avengers: Endgame (RGB Method)
![](samples/endgame.jpg)

### Baby Driver (RGB Method)
![](samples/baby_driver_rgb_method.jpg)


Steps used to create the ZoneImages.png file (in case any new images need to be added):

1.  Gather the desired normal image png files:
    a.  In File Explorer, open the DumpIcons mod output folder:  C:\Users\<username>\AppData\Local\Colossal Order\Cities_Skylines\DumpIcons
    b.  Search the folder for the image that best represents each zone row on the ZoneInfoPanel.
    c.  Choose the normal colored version of the image.  Avoid the Hovered, Focused, Pressed, and especially the Disabled versions.
    d.  Each image should have a transparent background which will be maintained in the steps below.
    e.  Copy each image file to the ZoneImages\Original project folder.
    f.  Leave each file with its original name so it can be easily identified.
    g.  In the VS project, add each new file to the Original folder.

2.  For each image that is not square:
    a.  In a browser, go to lunapic.com.
    b.  From the menu bar, click Upload.  The Quick Upload dialog is displayed.
    c.  Click on Choose File and choose the the image file to open from Zone Images\Original OR drag the file to the Choose File button.
    d.  The file is opened and ready for changes.
    e.  From the menu bar, click Edit -> Resize Canvas.  The Resize the Canvas dialog is displayed.
    f.  Leave the Canvas Color as transparent.
    g.  If the image height is less than width, set the height to be the same as the width.
    h.  If the image width is less than height, set the width to be the same as the height.
    i.  Set the Image Position to Center.
    j.  Leave the Tile Image box unchecked.
    k.  Click on Change Canvas Size.  The canvas is resized and the image is redisplayed.

3.  For each image that is not 40 x 40:
    a.  Upload the square image to lunapic.com as described above, or continue with the image from above that was made square.
    b.  From the menu bar, click Edit -> Scale Image.  The Scale Image dialog is displayed.
    c.  Set the Width to 40.  The Height should automatically change to 40.
    d.  Click Apply Scaling.  The image is resized and redisplayed.

4.  Save the adjusted normal image to a file:
    a.  From the menu bar, click File -> Save Image.  The Save Image dialog is displayed.
    b.  Click the Save as PNG button.  The updated file is downloaded to the PC named something like "imageedit_*.png".
    c.  Move the file from the Downloads folder to the ZoneImages\Adjusted project folder.
    d.  Name the file as the Zone enum string suffixed with "Normal.png", for example "ResidentialLowNormal.png".
    e.  In the VS project, add the Normal file to the Adjusted folder.

5.  For each image that is already 40 x 40:
    a.  Copy the file from the ZoneImages\Original project folder to the ZoneImages\Adjusted project folder.
    b.  Name the file as the Zone enum string suffixed with "Normal.png", for example "ResidentialLowNormal.png".
    c.  In the VS project, add the Normal file to the Adjusted folder.

6.  For each normal adjusted image, create a locked version of the image:
    a.  In a browser, go to onlinepngtools.com.
    b.  Find the option for Create a Grayscale PNG and click on it.  The png to grayscale converter dialog is displayed.
    c.  Click on Import from file and choose the normal image file to open from ZoneImages\Adjusted OR drag the file to the png area.
    d.  The file is imported and a default grayscale image is shown.
    e.  Under Options, select HDTV Formula and uncheck Use Custom Weights.  The options are applied as they are changed.
    f.  For Commercial Tourism and Leisure, click Use Custom Weights and set each weight to 0.2.  This makes these two images appear more like the others.
    g.  Under Grayscaled png, click on Save As... then click on Download.  The updated file is downloaded to the PC named "output-onlinepngtools.png".
    h.  Move the file from the Downloads folder to the ZoneImages\Adjusted project folder.
    i.  Name the file as the Zone enum string suffixed with "Locked.png", for example "ResidentialLowLocked.png".
    j.  In the VS project, add the Locked file to the Adjusted folder.

7.  Prevent individual image files from being included in the build:
    a.  In the VS project, select all files under ZoneImages/Original.  Set the Build Action to None and make sure Copy to Output Directory is set to Do not copy.
    b.  In the VS project, select all files under ZoneImages/Adjusted.  Set the Build Action to None and make sure Copy to Output Directory is set to Do not copy.

8.  Construct the ZoneImages.png file from all the Normal and Locked adjusted files:
    a.  The images will be arranged horizontally in the same order as the Zone enum with each Normal followed by its corresponding Locked.
    b.  In a browser, go to filesmerge.com.
    c.  Click on Merge Images.  The Merge Image dialog is displayed.
    d.  In the box for Drag and drop files here, drag and drop each file from ZoneImages\Adjusted one at a time in the correct order.
    e.  The correct order is the order of the Zones enum with Normal then Locked for each enum entry.  Each file will be appended to the file list.
    f.  Under Merge Options, select Merge Horizontally and change Output format to PNG.
    g.  Click the Merge button and wait for it to complete.
    h.  Click the Click to Download it link.  The merged file is downloaded to the PC named "merge_from_ofoct.png".
    i.  Move the file from the Downloads folder to the main project folder.
    j.  Delete the existing ZoneImages.png file.
    k.  Rename the newly downloaded file to ZoneImages.png.
    l.  The mod automatically computes the expected image size based on the number of entries in the Zones enum.
